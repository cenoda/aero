using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Git;
using Aero.Models.Project;
using Aero.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the file explorer sidebar. Owns the single workspace tree
/// and populates it lazily on expand — opening a folder only enumerates its
/// direct children, and each subdirectory's contents load when first expanded.
/// This keeps the load bounded regardless of workspace depth.
/// </summary>
public class FileExplorerViewModel : ReactiveObject, IDisposable
{
    private readonly IFileSystemService _fileSystem;
    private readonly IProjectLoader _projectLoader;
    private readonly IDocumentManagementService _documentManager;
    private readonly IFileSystemWatcherService _watcher;
    private readonly IMessageBus _bus;

    // Stored handlers for unsubscribe in Dispose()
    private Action<FolderOpened>? _folderOpenedHandler;
    private Action<FolderChanged>? _folderChangedHandler;
    private Action<GitStatusChanged>? _gitStatusChangedHandler;
    private Action<GitRepositoryChanged>? _gitRepositoryChangedHandler;

    // Cancellation source for the in-flight root load (null when idle).
    private CancellationTokenSource? _loadCts;

    // Per-node cancellation sources for in-flight child expansions. Cleared
    // when the load completes. Bounded: at most one entry per concurrently
    // loading node — the dictionary is the source of truth.
    private readonly ConcurrentDictionary<FileExplorerNodeViewModel, CancellationTokenSource> _childLoadCts = new();

    // Project roots discovered at root-load time, cached so child expansion
    // can match file entries without re-walking the workspace.
    private IReadOnlyList<ProjectInfo> _rootProjects = Array.Empty<ProjectInfo>();

    private bool _disposed;

    [Reactive] public string? RootPath { get; set; }
    [Reactive] public bool HasRootPath { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public FileExplorerNodeViewModel? SelectedNode { get; set; }
    [Reactive] public string StatusText { get; set; } = "No folder open";
    [Reactive] public bool HasGitRepository { get; set; }

    /// <summary>Top-level tree nodes. Empty until <see cref="LoadFolderAsync"/> runs.</summary>
    public ObservableCollection<FileExplorerNodeViewModel> RootNodes { get; } = new();

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSelectedFileCommand { get; }

    // Context-menu commands. Each takes the right-clicked node as parameter,
    // or null when triggered without a context node.
    public ReactiveCommand<FileExplorerNodeViewModel?, Unit> NewFileCommand { get; }
    public ReactiveCommand<FileExplorerNodeViewModel?, Unit> NewFolderCommand { get; }
    public ReactiveCommand<FileExplorerNodeViewModel?, Unit> RenameCommand { get; }
    public ReactiveCommand<FileExplorerNodeViewModel?, Unit> DeleteCommand { get; }

    public FileExplorerViewModel(
        IFileSystemService fileSystem,
        IProjectLoader projectLoader,
        IDocumentManagementService documentManager,
        IFileSystemWatcherService watcher,
        IMessageBus bus)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _projectLoader = projectLoader ?? throw new ArgumentNullException(nameof(projectLoader));
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        OpenSelectedFileCommand = ReactiveCommand.CreateFromTask(OpenSelectedFileAsync);
        NewFileCommand = ReactiveCommand.CreateFromTask<FileExplorerNodeViewModel?>(OnNewFileAsync);
        NewFolderCommand = ReactiveCommand.CreateFromTask<FileExplorerNodeViewModel?>(OnNewFolderAsync);
        RenameCommand = ReactiveCommand.CreateFromTask<FileExplorerNodeViewModel?>(OnRenameAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask<FileExplorerNodeViewModel?>(OnDeleteAsync);

        // Subscribe to folder-opened messages from the shell (File → Open Folder).
        _folderOpenedHandler = msg =>
        {
            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] Received FolderOpened: {msg.Path}\n");
            _ = LoadFolderAsync(msg.Path);
            try
            {
                _watcher.Watch(msg.Path);
            }
            catch (Exception ex)
            {
                // Watch can fail (deleted folder, permissions, inotify limits).
                // Surface it so the user knows auto-refresh is off; manual refresh
                // remains available.
                _bus.Publish(new StatusMessage(
                    $"Could not watch folder: {ex.Message}. Manual refresh is still available."));
            }
        };
        _bus.Subscribe(_folderOpenedHandler);

        // Subscribe to debounced filesystem changes and refresh the tree.
        _folderChangedHandler = msg => _ = OnFolderChangedAsync(msg);
        _bus.Subscribe(_folderChangedHandler);

        // Subscribe to Git status changes for modified-file indicators
        _gitStatusChangedHandler = msg => OnGitStatusChanged(msg);
        _bus.Subscribe(_gitStatusChangedHandler);

        // Subscribe to repository detection to sync HasGitRepository state
        _gitRepositoryChangedHandler = msg => OnGitRepositoryChanged(msg);
        _bus.Subscribe(_gitRepositoryChangedHandler);
    }

    /// <summary>
    /// Load and display the tree for the given folder. Cancels any in-flight
    /// root or child load before starting. Runs enumeration off the UI thread.
    ///
    /// Only the direct children of <paramref name="path"/> are enumerated —
    /// subdirectories are populated lazily by
    /// <see cref="EnsureChildrenLoadedAsync"/>. This is the fix for the R3.2
    /// unbounded-eager-load hazard: opening a large tree no longer walks every
    /// file and folder synchronously.
    /// </summary>
    public async Task LoadFolderAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            return;

        File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] LoadFolderAsync: {path}\n");

        // Cancel any in-flight root load AND any in-flight child expansions.
        // The new load replaces the entire tree — stale child loads would
        // populate nodes that are about to be discarded.
        CancelAllInFlightLoads();

        var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Interlocked.Exchange(ref _loadCts, newCts);
        var token = newCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        StatusText = $"Loading {path}…";

        try
        {
            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] Task.Run started: {path}\n");
            // Run the synchronous enumeration off the UI thread. The
            // FileSystemService is intentionally not self-offloading (see
            // FileSystemService.cs comments), so we own that responsibility.
            var (nodes, projects) = await Task.Run(
                () => BuildRootLevelAsync(path, token),
                token);

            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] Task.Run completed: {nodes.Count} nodes\n");
            // If a newer load superseded us while we awaited, drop this result.
            token.ThrowIfCancellationRequested();

            // Replace the tree on the UI thread. ObservableCollection is not
            // thread-safe; collection mutations must happen here.
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);

            _rootProjects = projects;

            // Normalize the root path (R1.5: future path deduplication).
            RootPath = Path.GetFullPath(path);
            HasRootPath = true;
            StatusText = $"{nodes.Count} entries";
            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] LoadFolderAsync complete: {nodes.Count} entries\n");
        }
        catch (OperationCanceledException)
        {
            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] Cancelled: {path}\n");
            // Expected when a newer load supersedes us. Don't surface an error.
            StatusText = $"Load cancelled for {path}";
        }
        catch (Exception ex)
        {
            File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] Exception: {ex.Message}\n{ex.StackTrace}\n");
            ErrorMessage = ex.Message;
            StatusText = $"Load failed: {ex.Message}";
            // Clear any partial tree on hard failure so the UI doesn't show stale nodes.
            RootNodes.Clear();
            _rootProjects = Array.Empty<ProjectInfo>();
            RootPath = null;
            HasRootPath = false;
        }
        finally
        {
            // Only clear our CTS reference if it's still ours. A newer load may
            // have replaced it by now.
            if (Interlocked.CompareExchange(ref _loadCts, null, newCts) == newCts)
            {
                newCts.Dispose();
            }
            IsLoading = false;
        }
    }

    /// <summary>
    /// Ensure <paramref name="node"/>'s direct children are loaded. Called by
    /// the view when a directory node is expanded. Idempotent: subsequent
    /// calls on a loaded node are a no-op.
    /// </summary>
    public async Task EnsureChildrenLoadedAsync(FileExplorerNodeViewModel node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (!node.IsDirectory) return;
        if (node.AreChildrenLoaded) return;

        // Cancel any in-flight expansion of this node (rapid double-expand)
        // before starting a new one. Using TryRemove + a fresh CTS below
        // makes this safe even when the same node is expanded repeatedly.
        if (_childLoadCts.TryRemove(node, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _childLoadCts[node] = cts;
        var token = cts.Token;

        try
        {
            var entries = await Task.Run(
                () => _fileSystem.GetDirectoryEntriesAsync(node.FullPath, token),
                token);

            token.ThrowIfCancellationRequested();

            // Swap children on the UI thread. ObservableCollection is not
            // thread-safe. The placeholder (if present) is removed and the
            // real children added in one pass.
            node.Children.Clear();
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                node.Children.Add(CreateNodeForEntry(entry, parent: node));
            }

            // Only mark loaded AFTER children are visible — a cancelled load
            // leaves AreChildrenLoaded=false so the next expand can retry.
            node.AreChildrenLoaded = true;
        }
        catch (OperationCanceledException)
        {
            // A newer expansion or a parent re-load cancelled us. Children
            // remain in their previous state (placeholder + empty list). Do
            // NOT mark AreChildrenLoaded so a subsequent expand retries.
        }
        catch (Exception)
        {
            // Hard failure: keep the placeholder so the user can retry by
            // collapsing + expanding. The status bar would be cluttered if
            // we surfaced every child-load failure; the root load already
            // reports workspace-level failures.
        }
        finally
        {
            // Remove our CTS only if it's still ours (a newer call may have
            // replaced it).
            if (_childLoadCts.TryRemove(new KeyValuePair<FileExplorerNodeViewModel, CancellationTokenSource>(node, cts)))
            {
                cts.Dispose();
            }
        }
    }

    /// <summary>Open the currently selected file node in the editor.</summary>
    public async Task OpenSelectedFileAsync()
    {
        var selected = SelectedNode;
        if (selected == null || selected.IsDirectory)
            return;

        await OpenFileAsync(selected);
    }

    /// <summary>Open a single file node, normalizing the path first (R1.5).</summary>
    public async Task OpenFileAsync(FileExplorerNodeViewModel node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.IsDirectory) return;

        var normalizedPath = Path.GetFullPath(node.FullPath);
        await _documentManager.OpenDocumentAsync(normalizedPath);
    }

    /// <summary>Manually re-enumerate the current root folder.</summary>
    public Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(RootPath))
            return Task.CompletedTask;
        return LoadFolderAsync(RootPath);
    }

    // --- context-menu command handlers -----------------------------------

    /// <summary>
    /// Force-reload the children of <paramref name="dir"/> by resetting its
    /// loaded flag and re-running lazy-load. Use after create/rename/delete
    /// so the tree reflects the filesystem change.
    /// </summary>
    private async Task ForceReloadChildrenAsync(FileExplorerNodeViewModel dir)
    {
        dir.AreChildrenLoaded = false;
        await EnsureChildrenLoadedAsync(dir);
    }

    private async Task OnNewFileAsync(FileExplorerNodeViewModel? node)
    {
        // Resolve target directory:
        //   dir selected → inside it; file selected → parent dir; null → root.
        var parentDir = node?.IsDirectory == true
            ? node.FullPath
            : node?.Parent?.FullPath ?? RootPath;
        if (string.IsNullOrEmpty(parentDir))
            return;

        var tcs = new TaskCompletionSource<string?>();
        _bus.Publish(new PromptNewItem(parentDir, IsFile: true, result => tcs.SetResult(result)));
        var name = await tcs.Task;
        if (name == null)
            return; // Cancelled

        await _fileSystem.CreateFileAsync(parentDir, name);

        // Refresh the directory that should now contain the new file.
        var targetDir = node?.IsDirectory == true ? node : node?.Parent;
        if (targetDir != null)
        {
            targetDir.IsExpanded = true;
            await ForceReloadChildrenAsync(targetDir);
        }
        else if (!string.IsNullOrEmpty(RootPath))
        {
            await LoadFolderAsync(RootPath);
        }
    }

    private async Task OnNewFolderAsync(FileExplorerNodeViewModel? node)
    {
        var parentDir = node?.IsDirectory == true
            ? node.FullPath
            : node?.Parent?.FullPath ?? RootPath;
        if (string.IsNullOrEmpty(parentDir))
            return;

        var tcs = new TaskCompletionSource<string?>();
        _bus.Publish(new PromptNewItem(parentDir, IsFile: false, result => tcs.SetResult(result)));
        var name = await tcs.Task;
        if (name == null)
            return;

        await _fileSystem.CreateDirectoryAsync(parentDir, name);

        var targetDir = node?.IsDirectory == true ? node : node?.Parent;
        if (targetDir != null)
        {
            targetDir.IsExpanded = true;
            await ForceReloadChildrenAsync(targetDir);
        }
        else if (!string.IsNullOrEmpty(RootPath))
        {
            await LoadFolderAsync(RootPath);
        }
    }

    private async Task OnRenameAsync(FileExplorerNodeViewModel? node)
    {
        if (node == null) return;

        var tcs = new TaskCompletionSource<string?>();
        _bus.Publish(new PromptRename(node.FullPath, result => tcs.SetResult(result)));
        var newName = await tcs.Task;
        if (string.IsNullOrEmpty(newName) || newName == node.Name)
            return; // No change or cancelled

        await _fileSystem.RenameAsync(node.FullPath, newName);

        // Refresh the parent so the old name disappears and the new appears.
        var parent = node.Parent;
        if (parent != null)
        {
            await ForceReloadChildrenAsync(parent);
        }
        else if (!string.IsNullOrEmpty(RootPath))
        {
            await LoadFolderAsync(RootPath);
        }
    }

    private async Task OnDeleteAsync(FileExplorerNodeViewModel? node)
    {
        if (node == null) return;

        var tcs = new TaskCompletionSource<bool>();
        _bus.Publish(new ConfirmDelete(node.FullPath, result => tcs.SetResult(result)));
        var confirmed = await tcs.Task;
        if (!confirmed) return;

        await _fileSystem.DeleteAsync(node.FullPath);

        // Refresh the parent so the deleted item disappears from the tree.
        var parent = node.Parent;
        if (parent != null)
        {
            await ForceReloadChildrenAsync(parent);
        }
        else if (!string.IsNullOrEmpty(RootPath))
        {
            await LoadFolderAsync(RootPath);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        CancelAllInFlightLoads();

        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
        if (_folderChangedHandler != null)
            _bus.Unsubscribe<FolderChanged>(_folderChangedHandler);
        if (_gitStatusChangedHandler != null)
            _bus.Unsubscribe<GitStatusChanged>(_gitStatusChangedHandler);
        if (_gitRepositoryChangedHandler != null)
            _bus.Unsubscribe<GitRepositoryChanged>(_gitRepositoryChangedHandler);

        // Stop watching. The watcher is a DI singleton; the container disposes
        // it on app exit, so the VM only needs to release its own subscription.
        _watcher.StopWatching();
    }

    /// <summary>
    /// Handle a <see cref="FolderChanged"/> message. The message may fire from a
    /// non-UI thread (the watcher debounce timer), so the refresh is marshalled
    /// onto <see cref="Avalonia.Threading.Dispatcher.UIThread"/> when available.
    /// </summary>
    private async Task OnFolderChangedAsync(FolderChanged msg)
    {
        if (string.IsNullOrEmpty(RootPath))
            return;

        // Defensive: ignore messages for a different root (e.g. around rapid
        // folder switches). Both paths are normalized before being stored.
        if (!string.Equals(msg.Path, RootPath, StringComparison.Ordinal))
            return;

        try
        {
            var dispatcher = GetUiDispatcher();
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(RefreshAsync);
            }
            else
            {
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FileExplorerViewModel] FolderChanged refresh failed: {ex}");
        }
    }

    /// <summary>
    /// Handle GitStatusChanged: update GitStatusGlyph on all nodes.
    /// Walks the tree recursively, matching FullPath against staged/unstaged files.
    /// </summary>
    private void OnGitStatusChanged(GitStatusChanged msg)
    {
        if (!HasGitRepository)
            return;

        // Build lookup sets for staged and unstaged files
        var stagedPaths = msg.StagedFiles
            .Select(f => Path.Combine(msg.WorkspacePath, f.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unstagedPaths = msg.UnstagedFiles
            .Select(f => Path.Combine(msg.WorkspacePath, f.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Update glyphs on root nodes
        UpdateGitGlyphsRecursive(RootNodes, stagedPaths, unstagedPaths);
    }

    /// <summary>
    /// Recursively update GitStatusGlyph on a collection of nodes.
    /// </summary>
    private void UpdateGitGlyphsRecursive(
        IEnumerable<FileExplorerNodeViewModel> nodes,
        HashSet<string> stagedPaths,
        HashSet<string> unstagedPaths)
    {
        foreach (var node in nodes)
        {
            // Skip placeholder
            if (node.IsPlaceholder)
                continue;

            var fullPath = node.FullPath;
            if (!string.IsNullOrEmpty(fullPath))
            {
                // Normalize for comparison
                var normalizedPath = Path.GetFullPath(fullPath);

                if (stagedPaths.Contains(normalizedPath))
                {
                    node.GitStatusGlyph = "A";
                }
                else if (unstagedPaths.Contains(normalizedPath))
                {
                    node.GitStatusGlyph = "M";
                }
                else
                {
                    node.GitStatusGlyph = "";
                }
            }

            // Recurse into children if loaded
            if (node.AreChildrenLoaded && node.Children.Count > 0)
            {
                UpdateGitGlyphsRecursive(node.Children, stagedPaths, unstagedPaths);
            }
        }
    }

    /// <summary>
    /// Handle GitRepositoryChanged: sync HasGitRepository and clear glyphs when repo closes.
    /// </summary>
    private void OnGitRepositoryChanged(GitRepositoryChanged msg)
    {
        HasGitRepository = msg.HasRepository;

        // Clear all glyphs when repository is closed
        if (!msg.HasRepository)
        {
            ClearGitGlyphsRecursive(RootNodes);
        }
    }

    /// <summary>
    /// Recursively clear GitStatusGlyph on a collection of nodes.
    /// </summary>
    private void ClearGitGlyphsRecursive(IEnumerable<FileExplorerNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsPlaceholder)
                continue;

            node.GitStatusGlyph = "";

            if (node.AreChildrenLoaded && node.Children.Count > 0)
            {
                ClearGitGlyphsRecursive(node.Children);
            }
        }
    }

    /// <summary>
    /// Return the Avalonia UI-thread dispatcher when running inside the app.
    /// Returns <c>null</c> in unit tests (no Avalonia application), allowing
    /// the refresh to run directly on the test thread.
    /// </summary>
    private static Avalonia.Threading.Dispatcher? GetUiDispatcher()
    {
        if (Avalonia.Application.Current == null)
            return null;
        try
        {
            return Avalonia.Threading.Dispatcher.UIThread;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // --- tree construction ---------------------------------------------

    /// <summary>
    /// Enumerate <paramref name="rootPath"/>'s direct children only. For each
    /// child directory, the placeholder child is added so the TreeView shows
    /// the expander arrow; the actual grandchildren load on demand.
    /// </summary>
    private async Task<(IReadOnlyList<FileExplorerNodeViewModel> Nodes, IReadOnlyList<ProjectInfo> Projects)>
        BuildRootLevelAsync(string rootPath, CancellationToken ct)
    {
        File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] BuildRootLevelAsync start: {rootPath}\n");
        // Project roots discovered at the workspace top level. Cached on the
        // VM for child expansions to reuse.
        var projects = _projectLoader.DetectProjects(rootPath, ct);

        File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] DetectProjects done, getting entries\n");
        var entries = await _fileSystem.GetDirectoryEntriesAsync(rootPath, ct);

        File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] GetDirectoryEntriesAsync done: {entries.Count} entries\n");
        var nodes = new List<FileExplorerNodeViewModel>(entries.Count);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            nodes.Add(CreateNodeForEntry(entry, projects));
        }
        File.AppendAllText("/tmp/aero-debug.log", $"[FileExplorerViewModel] BuildRootLevelAsync done: {nodes.Count} nodes\n");
        return (nodes, projects);
    }

    /// <summary>
    /// Build a node for a single <see cref="FileSystemEntry"/>. Directories
    /// get the placeholder child so the TreeView shows the expander arrow.
    /// Sets <see cref="FileExplorerNodeViewModel.Owner"/> and, when a
    /// <paramref name="parent"/> is provided,
    /// <see cref="FileExplorerNodeViewModel.Parent"/>.
    /// </summary>
    private FileExplorerNodeViewModel CreateNodeForEntry(
        FileSystemEntry entry,
        IReadOnlyList<ProjectInfo>? projects = null,
        FileExplorerNodeViewModel? parent = null)
    {
        var node = new FileExplorerNodeViewModel(
            entry.Name,
            entry.FullPath,
            entry.Kind == FileSystemEntryKind.Directory,
            IconFor(entry, projects ?? _rootProjects))
        {
            Owner = this,
            Parent = parent,
        };

        if (entry.Kind == FileSystemEntryKind.Directory)
        {
            // Mark as not-yet-loaded and seed the placeholder so the TreeView
            // renders an expander arrow. EnsureChildrenLoadedAsync will swap
            // this out when the directory is expanded.
            node.AreChildrenLoaded = false;
            node.Children.Add(FileExplorerNodeViewModel.PlaceholderChild);
        }

        return node;
    }

    private void CancelAllInFlightLoads()
    {
        // Root load
        var oldRootCts = Interlocked.Exchange(ref _loadCts, null);
        oldRootCts?.Cancel();
        oldRootCts?.Dispose();

        // Child loads — snapshot then clear so we don't fight a concurrent
        // EnsureChildrenLoadedAsync that's adding entries.
        foreach (var kvp in _childLoadCts)
        {
            if (_childLoadCts.TryRemove(kvp.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    private static string IconFor(FileSystemEntry entry, IReadOnlyList<ProjectInfo> projects)
    {
        _ = projects; // Retained for future multi-root workspace detection
        if (entry.Kind == FileSystemEntryKind.Directory)
            return "Folder";
        return IconResolver.GetIconKey(entry.FullPath);
    }
}
