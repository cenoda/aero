using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Editor;
using Aero.Models.Settings;
using Aero.Services;
using Aero.Services.Build;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using IMessageBus = Aero.Core.IMessageBus;
using GitStatusChanged = Aero.Core.GitStatusChanged;
using GitBranchChanged = Aero.Core.GitBranchChanged;

namespace Aero.ViewModels;

/// <summary>
/// Top-level ViewModel for the main application window.
/// Owns the overall layout state: active panel, status bar text, window title, etc.
/// </summary>
public class ShellViewModel : ReactiveObject, IDisposable
{
    private readonly IMessageBus _bus;
    private readonly IDocumentManagementService _documentManager;
    private readonly EditorViewModel _editorViewModel;
    private readonly FileExplorerViewModel _fileExplorerViewModel;
    private readonly ProblemsViewModel _problemsViewModel;
    private readonly OutputViewModel _outputViewModel;
    private readonly BuildServiceFactory _buildServiceFactory;
    private readonly DiagnosticStore _diagnosticStore;
    private readonly GitViewModel _gitViewModel;
    private readonly ISettingsService _settingsService;
    private IBuildService? _buildService;
    private CancellationTokenSource? _buildCts;

    // Stored handlers for unsubscribe
    private Action<FolderOpened>? _folderOpenedHandler;
    private Action<StatusMessage>? _statusMessageHandler;
    private Action<ActiveDocumentChanged>? _activeDocumentChangedHandler;
    private Action<DocumentSaved>? _documentSavedHandler;
    private Action<GitStatusChanged>? _gitStatusChangedHandler;
    private Action<GitBranchChanged>? _gitBranchChangedHandler;
    private bool _disposed;
    private string? _workspacePath;

    [Reactive] public string StatusText { get; set; } = "Aero IDE";
    [Reactive] public string WindowTitle { get; set; } = "Aero";
    [Reactive] public bool IsSidebarVisible { get; set; } = true;
    [Reactive] public int ActiveSidebarTabIndex { get; set; }
    [Reactive] public int ActiveBottomTabIndex { get; set; }
    [Reactive] public bool IsBottomPanelVisible { get; set; }

    // Window state — persisted via ISettingsService (Phase 8.7)
    [Reactive] public double WindowX { get; set; }
    [Reactive] public double WindowY { get; set; }
    [Reactive] public double WindowWidth { get; set; } = 1200;
    [Reactive] public double WindowHeight { get; set; } = 800;
    [Reactive] public bool IsWindowMaximized { get; set; }

    // ViewModels
    public EditorViewModel EditorViewModel => _editorViewModel;
    public FileExplorerViewModel FileExplorerViewModel => _fileExplorerViewModel;
    public ProblemsViewModel ProblemsViewModel => _problemsViewModel;
    public OutputViewModel OutputViewModel => _outputViewModel;
    public GitViewModel GitViewModel => _gitViewModel;

    // Commands
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> FindCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleSidebarCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSidebarTabCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleProblemsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> NextTabCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousTabCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> BuildCommand { get; }

public ShellViewModel(
        IMessageBus bus,
        IDocumentManagementService documentManager,
        EditorViewModel editorViewModel,
        FileExplorerViewModel fileExplorerViewModel,
        ProblemsViewModel problemsViewModel,
        OutputViewModel outputViewModel,
        BuildServiceFactory buildServiceFactory,
        DiagnosticStore diagnosticStore,
        GitViewModel gitViewModel,
        ISettingsService settingsService)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _editorViewModel = editorViewModel ?? throw new ArgumentNullException(nameof(editorViewModel));
        _fileExplorerViewModel = fileExplorerViewModel ?? throw new ArgumentNullException(nameof(fileExplorerViewModel));
        _problemsViewModel = problemsViewModel ?? throw new ArgumentNullException(nameof(problemsViewModel));
        _outputViewModel = outputViewModel ?? throw new ArgumentNullException(nameof(outputViewModel));
        _buildServiceFactory = buildServiceFactory ?? throw new ArgumentNullException(nameof(buildServiceFactory));
        _diagnosticStore = diagnosticStore ?? throw new ArgumentNullException(nameof(diagnosticStore));
        _gitViewModel = gitViewModel ?? throw new ArgumentNullException(nameof(gitViewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize commands
        NewFileCommand = ReactiveCommand.Create(NewFile);
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        OpenFolderCommand = ReactiveCommand.CreateFromTask(OpenFolderAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsAsync);
        CloseFileCommand = ReactiveCommand.Create(CloseFile);
        ExitCommand = ReactiveCommand.CreateFromTask(ExitAsync);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        FindCommand = ReactiveCommand.Create(Find);
        ReplaceCommand = ReactiveCommand.Create(Replace);
        ToggleSidebarCommand = ReactiveCommand.Create(ToggleSidebar);
        ToggleSidebarTabCommand = ReactiveCommand.Create(ToggleSidebarTab);
        ToggleOutputCommand = ReactiveCommand.Create(ToggleOutput);
        ToggleProblemsCommand = ReactiveCommand.Create(ToggleProblems);
        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);
        NextTabCommand = ReactiveCommand.Create(NextTab);
        PreviousTabCommand = ReactiveCommand.Create(PreviousTab);
        AboutCommand = ReactiveCommand.Create(About);
        BuildCommand = ReactiveCommand.CreateFromTask(BuildAsync);

        // Subscribe to messages — store handlers for unsubscribe
        _folderOpenedHandler = msg =>
        {
            StatusText = msg.Path;
            _workspacePath = msg.Path;
            _settingsService.AddRecentFolder(msg.Path);
            // Fire-and-forget — handles its own errors internally
            _ = SaveWorkspaceStateAsync();
        };
        _bus.Subscribe(_folderOpenedHandler);
        _statusMessageHandler = msg =>
        {
            // StatusMessage may be published from a background thread (e.g.
            // FileSystemWatcherService.OnError). Marshal the UI update onto the
            // UI thread so Avalonia's binding system does not throw.
            var dispatcher = GetUiDispatcher();
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Post(() => StatusText = msg.Text);
            }
            else
            {
                StatusText = msg.Text;
            }
        };
        _bus.Subscribe(_statusMessageHandler);
        _activeDocumentChangedHandler = OnActiveDocumentChanged;
        _bus.Subscribe(_activeDocumentChangedHandler);
        _documentSavedHandler = OnDocumentSaved;
        _bus.Subscribe(_documentSavedHandler);

        // Subscribe to Git messages (Phase 7)
        _gitStatusChangedHandler = OnGitStatusChanged;
        _bus.Subscribe(_gitStatusChangedHandler);
        _gitBranchChangedHandler = OnGitBranchChanged;
        _bus.Subscribe(_gitBranchChangedHandler);
    }

    private void OnGitStatusChanged(GitStatusChanged msg)
    {
        // Update local state if needed — GitViewModel already has this data
        // This handler exists so Shell can react to status changes if needed
    }

    private void OnGitBranchChanged(GitBranchChanged msg)
    {
        // Update branch display in status bar
        StatusText = $"Switched to {msg.BranchName}";
    }

    private void NewFile()
    {
        _editorViewModel.NewFile();
    }

    private async Task OpenFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window == null)
            return;

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                _bus.Publish(new FolderOpened(normalizedPath));
                StatusText = $"Opened folder: {normalizedPath}";
            }
            catch (Exception ex)
            {
                StatusText = $"Open folder failed: {ex.Message}";
            }
        }
    }

    private async Task OpenFileAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window == null)
            return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            try
            {
                await _editorViewModel.OpenFileAsync(file.Path.LocalPath);
            }
            catch (Exception ex)
            {
                // File read can fail (deleted, locked, no permission). Surface it
                // instead of letting the exception escape the command and crash the app.
                StatusText = $"Open failed: {ex.Message}";
            }
        }
    }

    private async Task SaveAsync()
    {
        if (EditorViewModel.ActiveTab?.Document == null)
            return;

        try
        {
            var saved = await _editorViewModel.SaveAsync();
            if (!saved)
            {
                await SaveAsWithDialogAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    private async Task SaveAsAsync() =>
        await SaveAsWithDialogAsync();

    private async Task SaveAsWithDialogAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window == null)
            return;

        var doc = EditorViewModel.ActiveTab?.Document;
        if (doc == null)
            return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File As",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            try
            {
                await _editorViewModel.SaveAsAsync(file.Path.LocalPath);
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
            }
        }
        else
        {
            StatusText = "Save cancelled";
        }
    }

    private void CloseFile()
    {
        _editorViewModel.CloseActiveTab();
    }

    private void NextTab()
    {
        var tabs = _editorViewModel.Tabs;
        if (tabs.Count == 0) return;
        if (_editorViewModel.ActiveTab == null)
        {
            _editorViewModel.ActivateTab(tabs[0]);
            return;
        }
        var idx = tabs.IndexOf(_editorViewModel.ActiveTab);
        var nextIdx = (idx + 1) % tabs.Count;
        _editorViewModel.ActivateTab(tabs[nextIdx]);
    }

    private void PreviousTab()
    {
        var tabs = _editorViewModel.Tabs;
        if (tabs.Count == 0) return;
        if (_editorViewModel.ActiveTab == null)
        {
            _editorViewModel.ActivateTab(tabs[^1]);
            return;
        }
        var idx = tabs.IndexOf(_editorViewModel.ActiveTab);
        var prevIdx = (idx - 1 + tabs.Count) % tabs.Count;
        _editorViewModel.ActivateTab(tabs[prevIdx]);
    }

    /// <summary>
    /// Exit the application, prompting for each dirty document one at a time.
    /// Mirrors the CloseActiveTab flow: iterate dirty docs sequentially, await
    /// each dialog response, then shut down only after all dialogs are resolved.
    /// </summary>
    private async Task ExitAsync()
    {
        if (!await CheckDirtyBeforeExitAsync())
            return;

        await SaveWorkspaceStateAsync();
        _documentManager.ClearLastDirtyState();
        Dispose();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mw)
                mw.MarkExitHandled();
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// Check all dirty documents and prompt the user before exit or window close.
    /// Returns <c>true</c> if exit should proceed, <c>false</c> if the user cancelled.
    /// </summary>
    public async Task<bool> CheckDirtyBeforeExitAsync()
    {
        var dirtyDocuments = _documentManager.Documents.Where(d => d.IsDirty).ToList();

        foreach (var doc in dirtyDocuments)
        {
            var tcs = new TaskCompletionSource<string>();

            _bus.Publish(new ConfirmDirtyClose(doc.DisplayName, response => tcs.TrySetResult(response)));

            // Guard against an unhandled confirmation message (no subscriber). Default to
            // Cancel so we do not discard unsaved work if the dialog infrastructure is missing.
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            var response = completedTask == tcs.Task ? await tcs.Task : DirtyCloseResponse.Cancel;

            if (response == DirtyCloseResponse.Save)
            {
                try
                {
                    var saved = await _documentManager.SaveDocumentAsync(doc);
                    if (!saved)
                    {
                        // Untitled document — try Save As dialog
                        var savedAs = await SaveAsDialogForDocAsync(doc);
                        if (!savedAs)
                            return false; // user cancelled Save As → cancel exit
                    }
                }
                catch (Exception ex)
                {
                    StatusText = $"Save failed: {ex.Message}";
                }
            }
            else if (response == DirtyCloseResponse.Cancel)
            {
                return false; // Stop exit — user cancelled
            }
            // DontSave: continue to next doc
        }

        return true;
    }

    /// <summary>
    /// Show a Save As file picker for the given document (not necessarily the active one).
    /// Returns <c>true</c> if the document was saved, <c>false</c> if the user cancelled.
    /// </summary>
    private async Task<bool> SaveAsDialogForDocAsync(TextDocument doc)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return false;

        var window = desktop.MainWindow;
        if (window == null)
            return false;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {doc.DisplayName} As",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            try
            {
                await _documentManager.SaveDocumentAsAsync(doc, file.Path.LocalPath);
                return true;
            }
            catch (Exception ex)
            {
                // Write failed — report it and cancel the exit rather than discarding work.
                StatusText = $"Save failed: {ex.Message}";
                return false;
            }
        }

        return false;
    }

    private void Undo()
    {
        _editorViewModel.Undo();
    }

    private void Redo()
    {
        _editorViewModel.Redo();
    }

    private void Find()
    {
        _editorViewModel.ShowFindReplace();
    }

    private void Replace()
    {
        _editorViewModel.ShowFindReplace(focusReplace: true);
    }

    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    private void ToggleSidebarTab()
    {
        // Toggle between Explorer (0) and Git (1) tabs
        ActiveSidebarTabIndex = ActiveSidebarTabIndex == 0 ? 1 : 0;
    }

    private void ToggleOutput()
    {
        // Show bottom panel and switch to Output tab (index 1)
        IsBottomPanelVisible = true;
        ActiveBottomTabIndex = 1;
    }

    private void ToggleProblems()
    {
        // Show bottom panel and switch to Problems tab (index 0)
        IsBottomPanelVisible = true;
        ActiveBottomTabIndex = 0;
    }

    private void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }

    private async Task BuildAsync()
    {
        if (string.IsNullOrEmpty(_workspacePath))
        {
            StatusText = "No workspace open";
            return;
        }

        _buildService = _buildServiceFactory.Detect(_workspacePath);
        if (_buildService == null)
        {
            StatusText = "No supported build system";
            return;
        }

        // Guard against concurrent builds (R2.5)
        if (_buildCts != null)
        {
            StatusText = "Build already in progress";
            return;
        }

        // Show output panel
        IsBottomPanelVisible = true;
        ActiveBottomTabIndex = 1; // Output tab

        // Clear previous output and build diagnostics
        _outputViewModel.Lines.Clear();
        _diagnosticStore.ClearSource("build");

        // Publish BuildStarted
        _bus.Publish(new BuildStarted(_workspacePath));
        StatusText = "Building...";

        _buildCts = new CancellationTokenSource();
        try
        {
            // Route through IBuildService.BuildAsync to get proper exit code and errors (R2.3)
            // Use AppendLine for thread-safe UI updates (R2.12)
            var options = new BuildOptions(_workspacePath);
            var result = await _buildService.BuildAsync(
                options,
                _outputViewModel.AppendLine,
                _buildCts.Token);

            // Publish BuildFinished with exit code from BuildResult
            _bus.Publish(new BuildFinished(result.ExitCode, ""));
            StatusText = result.Success ? "Build succeeded" : "Build failed";

            // Update DiagnosticStore with build errors/warnings (R2.6 - show warnings too)
            if (result.Errors.Count > 0)
            {
                var errorsByFile = result.Errors
                    .GroupBy(e => e.FilePath)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var kvp in errorsByFile)
                {
                    var fileUri = kvp.Key;
                    // Convert MSBuild's 1-based line/column to 0-based (R2.1)
                    var diags = kvp.Value.Select(e => new Diagnostic(
                        e.Severity == BuildSeverity.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                        fileUri,
                        new TextRange(e.Line - 1, e.Column - 1, e.Line - 1, e.Column - 1),
                        e.Message,
                        "build",
                        e.Code)).ToList();
                    _diagnosticStore.SetDiagnostics("build", fileUri, diags);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _bus.Publish(new BuildFinished(-1, "Cancelled"));
            StatusText = "Build cancelled";
        }
        finally
        {
            // Clean up CancellationTokenSource (R2.8)
            _buildCts?.Dispose();
            _buildCts = null;
        }
    }

    private void About()
    {
        // Show about dialog
    }

    private async Task SaveWorkspaceStateAsync()
    {
        var state = new WorkspaceState
        {
            LastFolderPath = _workspacePath,
            OpenFilePaths = _editorViewModel.Tabs
                .Select(t => t.FilePath).Where(p => p != null).ToList()!,
            ActiveTabIndex = _editorViewModel.ActiveTab != null
                ? _editorViewModel.Tabs.IndexOf(_editorViewModel.ActiveTab) : 0,
            Window = new WindowState
            {
                X = WindowX, Y = WindowY,
                Width = WindowWidth, Height = WindowHeight,
                IsMaximized = IsWindowMaximized
            },
            RecentFolders = _settingsService.GetRecentFolders().ToList()
        };
        try
        {
            await _settingsService.SaveWorkspaceStateAsync(state);
        }
        catch (Exception ex)
        {
            _bus.Publish(new StatusMessage($"Save failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Return the Avalonia UI-thread dispatcher when running inside the app.
    /// Returns <c>null</c> in unit tests (no Avalonia application), allowing
    /// the update to run directly on the test thread.
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

    private void OnActiveDocumentChanged(ActiveDocumentChanged msg)
    {
        var doc = msg.Document;
        if (doc == null)
        {
            WindowTitle = "Aero";
            StatusText = "Aero IDE";
            return;
        }

        WindowTitle = doc.IsDirty
            ? $"Aero - {doc.DisplayName} *"
            : $"Aero - {doc.DisplayName}";
        StatusText = doc.FilePath ?? doc.DisplayName;
    }

    private void OnDocumentSaved(DocumentSaved msg)
    {
        var doc = msg.Document;
        if (doc != EditorViewModel.ActiveTab?.Document)
            return;

        WindowTitle = $"Aero - {doc.DisplayName}";
        StatusText = doc.FilePath ?? doc.DisplayName;
    }

    /// <summary>Dispose message bus subscriptions to prevent stale-handler leaks.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
        if (_statusMessageHandler != null)
            _bus.Unsubscribe<StatusMessage>(_statusMessageHandler);
        if (_activeDocumentChangedHandler != null)
            _bus.Unsubscribe<ActiveDocumentChanged>(_activeDocumentChangedHandler);
        if (_documentSavedHandler != null)
            _bus.Unsubscribe<DocumentSaved>(_documentSavedHandler);
        if (_gitStatusChangedHandler != null)
            _bus.Unsubscribe<GitStatusChanged>(_gitStatusChangedHandler);
        if (_gitBranchChangedHandler != null)
            _bus.Unsubscribe<GitBranchChanged>(_gitBranchChangedHandler);

        _outputViewModel.Dispose();
        _gitViewModel.Dispose();
    }
}
