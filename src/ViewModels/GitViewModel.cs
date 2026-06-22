using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Git;
using Aero.Services.Git;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using IMessageBus = Aero.Core.IMessageBus;
using DocMsg = Aero.Core;
using GitFileStatus = Aero.Models.Git.GitFileStatus;
using GitFileStatusKind = Aero.Models.Git.GitFileStatusKind;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the Git panel.
/// </summary>
public class GitViewModel : ReactiveObject, IDisposable
{
    private readonly IMessageBus _bus;
    private readonly GitServiceFactory _factory;
    private IGitService? _gitService;
    private string? _workspacePath;
    private Action<FolderOpened>? _folderOpenedHandler;
    private Action<FolderChanged>? _folderChangedHandler;
    private CancellationTokenSource? _statusRefreshCts;
    private GitWatcher? _gitWatcher;
    private bool _disposed;

    // R1.3: Debounce/cooldown for status refresh
    // Issue #9 fix: Use Stopwatch for monotonic time (immune to system clock adjustments)
    private readonly Stopwatch _refreshCooldownStopwatch = new();
    private readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(1);

    [Reactive] public bool HasGitRepository { get; set; }
    [Reactive] public string CurrentBranch { get; set; } = "(no branch)";
    [Reactive] public bool IsDirty { get; set; }
    [Reactive] public string CommitMessage { get; set; } = string.Empty;
    [Reactive] public bool IsRefreshing { get; set; }
    [Reactive] public string StatusText { get; set; } = "No Git repository";
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public GitBranchInfo? SelectedBranch { get; set; }

    /// <summary>ViewModel for the branch graph tab.</summary>
    [Reactive] public GitGraphViewModel GitGraphViewModel { get; set; } = new();

    /// <summary>Staged changes (ready to commit).</summary>
    public ObservableCollection<GitFileStatusViewModel> StagedChanges { get; } = new();

    /// <summary>Unstaged changes (not yet staged).</summary>
    public ObservableCollection<GitFileStatusViewModel> UnstagedChanges { get; } = new();

    /// <summary>Available branches for checkout.</summary>
    public ObservableCollection<GitBranchInfo> Branches { get; } = new();

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> StageAllCommand { get; }
    public ReactiveCommand<Unit, Unit> UnstageAllCommand { get; }
    public ReactiveCommand<string, Unit> StageFileCommand { get; }
    public ReactiveCommand<string, Unit> UnstageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<string, Unit> CheckoutBranchCommand { get; }
    public ReactiveCommand<string, Unit> DiffCommand { get; }

    public GitViewModel(IMessageBus bus, GitServiceFactory factory)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        // All commands are async to avoid blocking UI (R1.5)
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        StageAllCommand = ReactiveCommand.CreateFromTask(StageAllAsync);
        UnstageAllCommand = ReactiveCommand.CreateFromTask(UnstageAllAsync);
        StageFileCommand = ReactiveCommand.CreateFromTask<string>(StageFileAsync);
        UnstageFileCommand = ReactiveCommand.CreateFromTask<string>(UnstageFileAsync);
        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync);
        CheckoutBranchCommand = ReactiveCommand.CreateFromTask<string>(CheckoutBranchAsync);
        DiffCommand = ReactiveCommand.Create<string>(PublishDiffRequested);

        // Subscribe to folder-opened messages
        _folderOpenedHandler = msg => _ = OnFolderOpenedAsync(msg.Path);
        _bus.Subscribe(_folderOpenedHandler);

        // Subscribe to folder-changed for status refresh (debounced via service)
        _folderChangedHandler = msg => _ = OnFolderChangedAsync(msg);
        _bus.Subscribe(_folderChangedHandler);
    }

    /// <summary>
    /// Called when a folder is opened. Detects Git repository.
    /// </summary>
    private async Task OnFolderOpenedAsync(string path)
    {
        _workspacePath = path;
        _gitService = _factory.Detect(path);

        if (_gitService == null)
        {
            HasGitRepository = false;
            CurrentBranch = "(no branch)";
            IsDirty = false;
            StatusText = "No Git repository";

            // Publish repository detection so other VMs can sync their state
            _bus.Publish(new GitRepositoryChanged(path, false));
            return;
        }

        HasGitRepository = true;
        await RefreshStatusInternalAsync();

        // Load branch graph data (M7-G3)
        _ = GitGraphViewModel.LoadAsync(_gitService);

        // Start GitWatcher for auto-reload on external git operations (M8-W2)
        var gitDir = Path.Combine(path, ".git");
        _gitWatcher?.Dispose();
        _gitWatcher = new GitWatcher(gitDir, () => _ = RefreshStatusInternalAsync());

        // W2 fix: Surface IsWatching = false to the user
        if (!_gitWatcher.IsWatching)
            _bus.Publish(new StatusMessage("Git auto-reload unavailable (inotify limit). Use Refresh manually."));

        // Publish repository detection so other VMs can sync their state
        _bus.Publish(new GitRepositoryChanged(_workspacePath!, true));
    }

    /// <summary>
    /// Called when folder changes. Refreshes status with cooldown (R1.3).
    /// </summary>
    private async Task OnFolderChangedAsync(FolderChanged msg)
    {
        if (!HasGitRepository || string.IsNullOrEmpty(_workspacePath))
            return;

        // Ignore messages for different workspace
        if (!string.Equals(msg.Path, _workspacePath, StringComparison.Ordinal))
            return;

        // Issue #9 fix: Use Stopwatch for monotonic time check
        if (_refreshCooldownStopwatch.IsRunning && _refreshCooldownStopwatch.Elapsed < _refreshCooldown)
            return;

        await RefreshStatusInternalAsync();
    }

    /// <summary>
    /// Refresh Git status. Runs asynchronously.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (!HasGitRepository || _gitService == null)
            return;

        await RefreshStatusInternalAsync();
    }

    /// <summary>
    /// Internal status refresh with error handling.
    /// </summary>
    private async Task RefreshStatusInternalAsync()
    {
        if (_gitService == null)
            return;

        // Cancel any in-flight refresh
        _statusRefreshCts?.Cancel();
        _statusRefreshCts = new CancellationTokenSource();
        var token = _statusRefreshCts.Token;

        IsRefreshing = true;
        ErrorMessage = null;
        StatusText = "Refreshing...";

        try
        {
            // Issue #9 fix: Start monotonic stopwatch for cooldown tracking
            _refreshCooldownStopwatch.Restart();

            // Get status asynchronously
            var repoInfo = await _gitService.GetRepositoryInfoAsync(token);
            var statuses = await _gitService.GetStatusAsync(token);
            var branches = await _gitService.GetBranchesAsync(token);

            CurrentBranch = repoInfo.CurrentBranch;
            IsDirty = repoInfo.IsDirty;

            // Clear and populate collections on UI thread
            StagedChanges.Clear();
            UnstagedChanges.Clear();
            Branches.Clear();

            foreach (var status in statuses)
            {
                // Index status -> staged, workdir status -> unstaged
                var isStaged = status.StagingStatus != GitFileStatusKind.Unmodified;
                var isUnstaged = status.Status != GitFileStatusKind.Unmodified;

                if (isStaged || isUnstaged)
                {
                    var vm = new GitFileStatusViewModel(status);
                    if (isStaged)
                        StagedChanges.Add(vm);
                    if (isUnstaged)
                        UnstagedChanges.Add(vm);
                }
            }

            foreach (var branch in branches.Where(b => !b.IsRemote))
            {
                Branches.Add(branch);
            }

StatusText = IsDirty ? $"On {CurrentBranch} (dirty)" : $"On {CurrentBranch}";

            // Publish GitStatusChanged
            var stagedFiles = StagedChanges.Select(s => new GitFileStatus(
                s.FilePath, null, GitFileStatusKind.Staged, s.Status)).ToList();
            var unstagedFiles = UnstagedChanges.Select(s => new GitFileStatus(
                s.FilePath, null, s.StagingStatus, s.Status)).ToList();
            _bus.Publish(new GitStatusChanged(_workspacePath!, stagedFiles, unstagedFiles, CurrentBranch));
        }
        catch (OperationCanceledException)
        {
            // Expected when superseded by newer refresh
            StatusText = "Refresh cancelled";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>Stage all unstaged files.</summary>
    private async Task StageAllAsync()
    {
        if (_gitService == null)
            return;

        // Issue #8 fix: Batch stage all files then refresh once
        var files = UnstagedChanges.Select(f => f.FilePath).ToList();
        foreach (var file in files)
        {
            await _gitService.StageAsync(file, CancellationToken.None);
        }
        // Single refresh after all staging
        await RefreshStatusInternalAsync();
    }

    /// <summary>Unstage all staged files.</summary>
    private async Task UnstageAllAsync()
    {
        if (_gitService == null)
            return;

        // Issue #8 fix: Batch unstage all files then refresh once
        var files = StagedChanges.Select(f => f.FilePath).ToList();
        foreach (var file in files)
        {
            await _gitService.UnstageAsync(file, CancellationToken.None);
        }
        // Single refresh after all unstaging
        await RefreshStatusInternalAsync();
    }

    /// <summary>Stage a single file.</summary>
    public async Task StageFileAsync(string filePath)
    {
        if (_gitService == null)
            return;

        await _gitService.StageAsync(filePath, CancellationToken.None);
        await RefreshStatusInternalAsync();
    }

    /// <summary>Unstage a single file.</summary>
    public async Task UnstageFileAsync(string filePath)
    {
        if (_gitService == null)
            return;

        await _gitService.UnstageAsync(filePath, CancellationToken.None);
        await RefreshStatusInternalAsync();
    }

    /// <summary>Commit staged changes.</summary>
    private async Task CommitAsync()
    {
        if (_gitService == null || string.IsNullOrWhiteSpace(CommitMessage))
            return;

        // Get author from git config, fall back to system user if not configured
        var configKeys = new[] { "user.name", "user.email" };
        var configValues = await _gitService.GetConfigAsync(configKeys, CancellationToken.None);
        var authorName = !string.IsNullOrEmpty(configValues[0]) ? configValues[0] : Environment.UserName;
        var authorEmail = !string.IsNullOrEmpty(configValues[1]) ? configValues[1] : $"{Environment.UserName}@localhost";

        var result = await _gitService.CommitAsync(CommitMessage, authorName, authorEmail, CancellationToken.None);

        if (result.Success)
        {
            CommitMessage = string.Empty;
            await RefreshStatusInternalAsync();
            _bus.Publish(new StatusMessage($"Commit {result.Sha[..7]} created"));
        }
        else
        {
            ErrorMessage = result.Error ?? "Commit failed";
        }
    }

    /// <summary>Checkout a branch.</summary>
    public async Task CheckoutBranchAsync(string branchName)
    {
        if (_gitService == null || string.IsNullOrWhiteSpace(branchName))
            return;

        try
        {
            await _gitService.CheckoutAsync(branchName, CancellationToken.None);
            _bus.Publish(new GitBranchChanged(branchName));
            await RefreshStatusInternalAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot switch branch"))
        {
            // R1.7: Handle checkout conflicts gracefully
            ErrorMessage = ex.Message;
            _bus.Publish(new StatusMessage(ex.Message));
        }
    }

    /// <summary>
    /// Publishes a GitDiffRequested message for the given file path.
    /// </summary>
    private void PublishDiffRequested(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || _workspacePath == null)
            return;

        // Construct full path from workspace + relative file path
        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(_workspacePath, filePath);

        _bus.Publish(new DocMsg.GitDiffRequested(fullPath));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _statusRefreshCts?.Cancel();
        _statusRefreshCts?.Dispose();

        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
        if (_folderChangedHandler != null)
            _bus.Unsubscribe<FolderChanged>(_folderChangedHandler);

        // Dispose GitWatcher for auto-reload (M8-W2)
        _gitWatcher?.Dispose();
        _gitWatcher = null;

        // Issue #5 fix: Don't dispose factory - it's a DI singleton, managed by the container
        // The container will dispose it when the app exits
    }
}

/// <summary>
/// ViewModel for a single file status entry.
/// </summary>
public class GitFileStatusViewModel : ReactiveObject
{
    public string FilePath { get; }
    public GitFileStatusKind Status { get; }
    public GitFileStatusKind StagingStatus { get; }

    public string DisplayPath => FilePath.Split('/').Last();
    public string StatusIcon => Status switch
    {
        GitFileStatusKind.Modified => "~",
        GitFileStatusKind.Added => "+",
        GitFileStatusKind.Deleted => "-",
        GitFileStatusKind.Renamed => "R",
        GitFileStatusKind.Untracked => "?",
        _ => " "
    };

    public GitFileStatusViewModel(GitFileStatus status)
    {
        FilePath = status.FilePath;
        Status = status.Status;
        StagingStatus = status.StagingStatus;
    }
}
