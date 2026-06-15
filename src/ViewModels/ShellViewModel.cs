using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Aero.Core;
using Aero.Services;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// Top-level ViewModel for the main application window.
/// Owns the overall layout state: active panel, status bar text, window title, etc.
/// </summary>
public class ShellViewModel : ReactiveObject
{
    private readonly IMessageBus _bus;
    private readonly DocumentManager _documentManager;
    private readonly EditorViewModel _editorViewModel;

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
        ExitCommand = ReactiveCommand.Create(Exit);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        FindCommand = ReactiveCommand.Create(Find);
        ReplaceCommand = ReactiveCommand.Create(Replace);
        ToggleFileExplorerCommand = ReactiveCommand.Create(ToggleFileExplorer);
        ToggleTerminalCommand = ReactiveCommand.Create(ToggleTerminal);
        AboutCommand = ReactiveCommand.Create(About);

        // Subscribe to messages
        _bus.Subscribe<FolderOpened>(msg => StatusText = msg.Path);
        _bus.Subscribe<ActiveDocumentChanged>(OnActiveDocumentChanged);
        _bus.Subscribe<DocumentSaved>(OnDocumentSaved);
    }

private void NewFile()
    {
        _editorViewModel.NewFile();
    }

private async System.Threading.Tasks.Task OpenFileAsync()
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

private async System.Threading.Tasks.Task SaveAsync()
    {
        if (EditorViewModel.ActiveTab?.Document == null)
            return;

        var saved = await _editorViewModel.SaveAsync();
        if (!saved)
        {
            await SaveAsWithDialogAsync();
        }
    }

private async System.Threading.Tasks.Task SaveAsAsync() =>
        await SaveAsWithDialogAsync();

    private async System.Threading.Tasks.Task SaveAsWithDialogAsync()
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

private void Exit()
    {
        // Close the application
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
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
        _editorViewModel.ShowFindReplace();
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
            ? $"Aero - {doc.FileName} *"
            : $"Aero - {doc.FileName}";
        StatusText = doc.FilePath ?? doc.FileName;
    }

    private void OnDocumentSaved(DocumentSaved msg)
    {
        var doc = msg.Document;
        if (doc == null || doc != EditorViewModel.ActiveTab?.Document)
            return;

        WindowTitle = $"Aero - {doc.FileName}";
        StatusText = doc.FilePath ?? doc.FileName;
    }
}
