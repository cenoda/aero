using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Aero.Core;
using Aero.Models.Editor;
using Aero.Services;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// Top-level ViewModel for the main application window.
/// Owns the overall layout state: active panel, status bar text, window title, etc.
/// </summary>
public class ShellViewModel : ReactiveObject, IDisposable
{
    private readonly IMessageBus _bus;
    private readonly DocumentManager _documentManager;
    private readonly EditorViewModel _editorViewModel;

    // Stored handlers for unsubscribe
    private Action<FolderOpened>? _folderOpenedHandler;
    private Action<ActiveDocumentChanged>? _activeDocumentChangedHandler;
    private Action<DocumentSaved>? _documentSavedHandler;

[Reactive] public string StatusText { get; set; } = "Aero IDE";
    [Reactive] public string WindowTitle { get; set; } = "Aero";
    [Reactive] public bool IsFileExplorerVisible { get; set; } = true;
    [Reactive] public bool IsTerminalVisible { get; set; }

    // ViewModels
    public EditorViewModel EditorViewModel => _editorViewModel;

    // Commands
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> FindCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleFileExplorerCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleTerminalCommand { get; }
    public ReactiveCommand<Unit, Unit> NextTabCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousTabCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }

    public ShellViewModel(IMessageBus bus, DocumentManager documentManager, EditorViewModel editorViewModel)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _editorViewModel = editorViewModel ?? throw new ArgumentNullException(nameof(editorViewModel));

        // Initialize commands
        NewFileCommand = ReactiveCommand.Create(NewFile);
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsAsync);
        CloseFileCommand = ReactiveCommand.Create(CloseFile);
        ExitCommand = ReactiveCommand.CreateFromTask(ExitAsync);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        FindCommand = ReactiveCommand.Create(Find);
        ReplaceCommand = ReactiveCommand.Create(Replace);
        ToggleFileExplorerCommand = ReactiveCommand.Create(ToggleFileExplorer);
        ToggleTerminalCommand = ReactiveCommand.Create(ToggleTerminal);
        NextTabCommand = ReactiveCommand.Create(NextTab);
        PreviousTabCommand = ReactiveCommand.Create(PreviousTab);
        AboutCommand = ReactiveCommand.Create(About);

        // Subscribe to messages — store handlers for unsubscribe
        _folderOpenedHandler = msg => StatusText = msg.Path;
        _bus.Subscribe(_folderOpenedHandler);
        _activeDocumentChangedHandler = OnActiveDocumentChanged;
        _bus.Subscribe(_activeDocumentChangedHandler);
        _documentSavedHandler = OnDocumentSaved;
        _bus.Subscribe(_documentSavedHandler);
    }

private void NewFile()
    {
        _editorViewModel.NewFile();
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
            await _editorViewModel.OpenFileAsync(file.Path.LocalPath);
        }
    }

private async Task SaveAsync()
    {
        if (EditorViewModel.ActiveTab?.Document == null)
            return;

        var saved = await _editorViewModel.SaveAsync();
        if (!saved)
        {
            await SaveAsWithDialogAsync();
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
            await _editorViewModel.SaveAsAsync(file.Path.LocalPath);
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

            var response = await tcs.Task;

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
            await _documentManager.SaveDocumentAsAsync(doc, file.Path.LocalPath);
            return true;
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

    private void ToggleFileExplorer()
    {
        IsFileExplorerVisible = !IsFileExplorerVisible;
    }

    private void ToggleTerminal()
    {
        IsTerminalVisible = !IsTerminalVisible;
    }

    private void About()
    {
        // Show about dialog
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
        if (doc == null || doc != EditorViewModel.ActiveTab?.Document)
            return;

        WindowTitle = $"Aero - {doc.DisplayName}";
        StatusText = doc.FilePath ?? doc.DisplayName;
    }

    /// <summary>Dispose message bus subscriptions to prevent stale-handler leaks.</summary>
    public void Dispose()
    {
        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
        if (_activeDocumentChangedHandler != null)
            _bus.Unsubscribe<ActiveDocumentChanged>(_activeDocumentChangedHandler);
        if (_documentSavedHandler != null)
            _bus.Unsubscribe<DocumentSaved>(_documentSavedHandler);
    }
}
