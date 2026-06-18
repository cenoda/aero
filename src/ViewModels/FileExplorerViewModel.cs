using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Project;
using Aero.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the file explorer sidebar. Owns the single workspace tree,
/// builds it off the UI thread, and cancels any in-flight load when a new
/// folder is opened.
/// </summary>
public class FileExplorerViewModel : ReactiveObject, IDisposable
{
    private readonly IFileSystemService _fileSystem;
    private readonly IProjectLoader _projectLoader;
    private readonly IMessageBus _bus;

    // Stored handlers for unsubscribe in Dispose()
    private Action<FolderOpened>? _folderOpenedHandler;

    // Cancellation source for the in-flight load (null when idle).
    private CancellationTokenSource? _loadCts;
    private bool _disposed;

    [Reactive] public string? RootPath { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public FileExplorerNodeViewModel? SelectedNode { get; set; }
    [Reactive] public string StatusText { get; set; } = "No folder open";

    /// <summary>Top-level tree nodes. Empty until <see cref="LoadFolderAsync"/> runs.</summary>
    public ObservableCollection<FileExplorerNodeViewModel> RootNodes { get; } = new();

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public FileExplorerViewModel(
        IFileSystemService fileSystem,
        IProjectLoader projectLoader,
        IMessageBus bus)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _projectLoader = projectLoader ?? throw new ArgumentNullException(nameof(projectLoader));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        // Subscribe to folder-opened messages from the shell (File → Open Folder).
        _folderOpenedHandler = msg => _ = LoadFolderAsync(msg.Path);
        _bus.Subscribe(_folderOpenedHandler);
    }

    /// <summary>
    /// Load and display the tree for the given folder. Cancels any in-flight
    /// load before starting. Runs the enumeration off the UI thread.
    /// </summary>
    public async Task LoadFolderAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            return;

        // Cancel any previous load. Disposing the old CTS after creating the
        // new one ensures we don't tear down the new token by accident.
        var oldCts = Interlocked.Exchange(ref _loadCts, null);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Interlocked.Exchange(ref _loadCts, newCts);
        var token = newCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        StatusText = $"Loading {path}…";

        try
        {
            // Run the synchronous enumeration off the UI thread. The
            // FileSystemService is intentionally not self-offloading (see
            // FileSystemService.cs comments), so we own that responsibility.
            var nodes = await Task.Run(
                () => BuildTreeAsync(path, token),
                token);

            // If a newer load superseded us while we awaited, drop this result.
            token.ThrowIfCancellationRequested();

            // Replace the tree on the UI thread. ObservableCollection is not
            // thread-safe; collection mutations must happen here.
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);

            // Normalize the root path (R1.5: future path deduplication).
            RootPath = Path.GetFullPath(path);
            StatusText = $"{nodes.Count} entries";
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer load supersedes us. Don't surface an error.
            StatusText = $"Load cancelled for {path}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = $"Load failed: {ex.Message}";
            // Clear any partial tree on hard failure so the UI doesn't show stale nodes.
            RootNodes.Clear();
            RootPath = null;
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

    /// <summary>Manually re-enumerate the current root folder.</summary>
    public Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(RootPath))
            return Task.CompletedTask;
        return LoadFolderAsync(RootPath);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Cancel any in-flight load so it doesn't write to disposed state.
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
    }

    // --- tree construction ---------------------------------------------

    /// <summary>
    /// Build the full tree rooted at <paramref name="rootPath"/>. Synchronous
    /// helper called via <c>Task.Run</c> — does NOT touch the UI thread or
    /// any ObservableCollection.
    /// </summary>
    private async Task<IReadOnlyList<FileExplorerNodeViewModel>> BuildTreeAsync(
        string rootPath,
        CancellationToken ct)
    {
        // One-level-deep project roots so we can choose specific icons for them.
        var projects = _projectLoader.DetectProjects(rootPath, ct);

        // Recursive enumeration. We don't use a TreeNode abstraction because
        // the VM tree shape is shallow — directories always expand to their
        // direct children. Eager loading + IgnoreList keeps this bounded.
        var entries = await _fileSystem.GetDirectoryEntriesAsync(rootPath, ct);

        var nodes = new List<FileExplorerNodeViewModel>(entries.Count);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            nodes.Add(await BuildNodeAsync(entry, projects, ct));
        }
        return nodes;
    }

    private async Task<FileExplorerNodeViewModel> BuildNodeAsync(
        FileSystemEntry entry,
        IReadOnlyList<ProjectInfo> projects,
        CancellationToken ct)
    {
        var node = new FileExplorerNodeViewModel(
            entry.Name,
            entry.FullPath,
            entry.Kind == FileSystemEntryKind.Directory,
            IconFor(entry, projects));

        if (entry.Kind == FileSystemEntryKind.Directory)
        {
            // Eager enumeration: recurse to show nested folders/files in the
            // initial tree (PROJECT_PLAN §5.1). The IIgnoreList bounds the
            // walk — `node_modules`, `bin`, `obj`, etc. are skipped at the
            // service layer, so this stays tractable for normal workspaces.
            var children = await _fileSystem.GetDirectoryEntriesAsync(entry.FullPath, ct);
            foreach (var child in children)
            {
                ct.ThrowIfCancellationRequested();
                node.Children.Add(await BuildNodeAsync(child, projects, ct));
            }
        }

        return node;
    }

    private static string IconFor(FileSystemEntry entry, IReadOnlyList<ProjectInfo> projects)
    {
        if (entry.Kind == FileSystemEntryKind.Directory)
            return "Folder";

        // Highlight recognized project files with their own icon. ProjectInfo
        // matches by full path so this stays O(projects) per file.
        foreach (var p in projects)
        {
            if (string.Equals(p.Path, entry.FullPath, StringComparison.Ordinal))
            {
                return p.Kind switch
                {
                    ProjectKind.Solution => "MicrosoftVisualStudio",
                    ProjectKind.CSharpProject => "LanguageCsharp",
                    ProjectKind.NodeProject => "Nodejs",
                    _ => "FileDocument",
                };
            }
        }

        return "FileDocument";
    }
}
