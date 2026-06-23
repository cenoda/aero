using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aero.Core;
using Aero.Docking;
using Aero.Docking.DocumentViewModels;
using Aero.Docking.ToolViewModels;
using Aero.Services;
using Aero.ViewModels;
using Aero.Views;
using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Aero;

public partial class MainWindow : Window
{
    private IMessageBus? _bus;
    private ILayoutPersistenceService? _layoutPersistence;
    private Action<ConfirmDirtyClose>? _confirmDirtyCloseHandler;
    private Action<PromptNewItem>? _promptNewItemHandler;
    private Action<PromptRename>? _promptRenameHandler;
    private Action<ConfirmDelete>? _confirmDeleteHandler;
    private bool _exitHandled;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(ILayoutPersistenceService? layoutPersistence)
    {
        _layoutPersistence = layoutPersistence;
        InitializeComponent();
        Closing += OnClosing;

        PositionChanged += (_, args) =>
        {
            if (DataContext is ShellViewModel shell)
            {
                shell.WindowX = args.Point.X;
                shell.WindowY = args.Point.Y;
            }
        };
    }

    private void InitializeDockControl(ShellViewModel shell)
    {
        if (DockControl == null) return;

        // Try to restore saved layout, fall back to default
        var layout = _layoutPersistence?.Load();
        if (layout == null)
        {
            // CreateDefaultLayout() creates its own factory and calls InitLayout(root)
            layout = AeroDockFactory.CreateDefaultLayout();
        }

        // CRITICAL: Set InitializeFactory BEFORE Layout assignment.
        // When Layout is set, DockControl.OnPropertyChanged fires Initialize()
        // which checks InitializeFactory to set up locators (ContextLocator,
        // HostWindowLocator, etc.) that drag-and-drop needs.
        DockControl.InitializeFactory = true;

        // We already called InitLayout inside CreateDefaultLayout, so prevent double-init.
        DockControl.InitializeLayout = false;

        // Set the factory explicitly as a safety net for drag-and-drop wiring.
        DockControl.Factory = layout.Factory!;

        // Wire ViewModels to dock tools BEFORE assigning layout to DockControl.
        // DockControl.Layout setter triggers Initialize() which creates the visual
        // tree. The DataTemplates bind {Binding Context}, so Context must be
        // set before the visual tree is created.
        shell.ActiveLayout = layout;
        WireViewModels(layout, shell);

        // Now assign layout — triggers Initialize() → visual tree creation.
        DockControl.Layout = layout;
    }

    /// <summary>
    /// Walk the dock tree and set Context on each tool/document so
    /// DataTemplates bind directly to the correct ViewModel.
    /// Dock.Avalonia's DeferredContentControl uses Context as the DataContext.
    /// </summary>
    private static void WireViewModels(IDock dock, ShellViewModel shell)
    {
        if (dock.VisibleDockables == null) return;
        foreach (var child in dock.VisibleDockables)
        {
            switch (child)
            {
                case ExplorerTool explorer:
                    explorer.Context = shell.FileExplorerViewModel;
                    File.AppendAllText("/tmp/aero-debug.log", $"[MainWindow] Wired ExplorerTool.Context\n");
                    break;
                case GitTool git:
                    git.Context = shell.GitViewModel;
                    File.AppendAllText("/tmp/aero-debug.log", $"[MainWindow] Wired GitTool.Context\n");
                    break;
                case ProblemsTool problems:
                    problems.Context = shell.ProblemsViewModel;
                    File.AppendAllText("/tmp/aero-debug.log", $"[MainWindow] Wired ProblemsTool.Context\n");
                    break;
                case OutputTool output:
                    output.Context = shell.OutputViewModel;
                    File.AppendAllText("/tmp/aero-debug.log", $"[MainWindow] Wired OutputTool.Context\n");
                    break;
                case EditorDocument editor:
                    editor.Context = shell.EditorViewModel;
                    File.AppendAllText("/tmp/aero-debug.log", $"[MainWindow] Wired EditorDocument.Context\n");
                    break;
            }
            // Recurse into nested docks
            if (child is IDock childDock)
                WireViewModels(childDock, shell);
        }
    }

    /// <summary>
    /// Wire the bus subscription after construction. Avalonia's XAML loader requires
    /// a parameterless constructor, so DI of <see cref="IMessageBus"/> happens via
    /// this explicit initializer called from <c>App.axaml.cs</c>.
    /// </summary>
    public void Initialize(IMessageBus bus)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        _bus = bus;
        _confirmDirtyCloseHandler = OnConfirmDirtyClose;
        _bus.Subscribe(_confirmDirtyCloseHandler);

        _promptNewItemHandler = OnPromptNewItem;
        _bus.Subscribe(_promptNewItemHandler);

        _promptRenameHandler = OnPromptRename;
        _bus.Subscribe(_promptRenameHandler);

        _confirmDeleteHandler = OnConfirmDelete;
        _bus.Subscribe(_confirmDeleteHandler);

        // Initialize DockControl AFTER DataContext is set (from App.axaml.cs).
        // This ensures WireViewModels() has access to the ShellViewModel.
        if (DataContext is ShellViewModel shell)
            InitializeDockControl(shell);
    }

    /// <summary>
    /// Mark that the exit flow has already validated dirty documents (called by
    /// <see cref="ShellViewModel.ExitAsync"/> before <c>desktop.Shutdown()</c>).
    /// This prevents OnClosing from re-running the dirty-check prompt.
    /// </summary>
    internal void MarkExitHandled() => _exitHandled = true;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Save layout on close
        if (_layoutPersistence != null && DockControl?.Layout is IRootDock layout)
        {
            _layoutPersistence.Save(layout);
        }

        // If ExitAsync() already handled the dirty check, just clean up
        if (_exitHandled)
        {
            UnsubscribeBus();
            return;
        }

        // Check dirty documents before allowing OS window close (e.g. clicking "X")
        if (DataContext is ShellViewModel shell)
        {
            e.Cancel = true;
            var canExit = await shell.CheckDirtyBeforeExitAsync();
            if (canExit)
            {
                await shell.SaveWorkspaceStateAsync();
                _exitHandled = true;
                Close();
            }
        }
    }

    private void UnsubscribeBus()
    {
        if (_bus == null) return;

        if (_confirmDirtyCloseHandler != null)
        {
            _bus.Unsubscribe<ConfirmDirtyClose>(_confirmDirtyCloseHandler);
            _confirmDirtyCloseHandler = null;
        }

        if (_promptNewItemHandler != null)
        {
            _bus.Unsubscribe<PromptNewItem>(_promptNewItemHandler);
            _promptNewItemHandler = null;
        }

        if (_promptRenameHandler != null)
        {
            _bus.Unsubscribe<PromptRename>(_promptRenameHandler);
            _promptRenameHandler = null;
        }

        if (_confirmDeleteHandler != null)
        {
            _bus.Unsubscribe<ConfirmDelete>(_confirmDeleteHandler);
            _confirmDeleteHandler = null;
        }
    }

    private async void OnConfirmDirtyClose(ConfirmDirtyClose msg)
    {
        var result = await DirtyCloseDialog.ShowAsync(this, msg.FileName);
        msg.OnResponse(result ?? DirtyCloseResponse.Cancel);
    }

#pragma warning disable VSTHRD100 // async void is required for MessageBus handlers

    private async void OnPromptNewItem(PromptNewItem msg)
    {
        var title = msg.IsFile ? "New File" : "New Folder";
        var prompt = msg.IsFile
            ? "Enter a name for the new file:"
            : "Enter a name for the new folder:";
        var response = await TextInputDialog.ShowAsync(this, title, prompt);
        msg.OnResult(response);
    }

    private async void OnPromptRename(PromptRename msg)
    {
        var defaultName = System.IO.Path.GetFileName(msg.Path);
        var response = await TextInputDialog.ShowAsync(
            this, "Rename", "Enter a new name:", defaultName);
        msg.OnResult(response);
    }

    private async void OnConfirmDelete(ConfirmDelete msg)
    {
        // Concern #4: map null (dialog cancelled / window closed) → false
        var name = System.IO.Path.GetFileName(msg.Path);
        var response = await ConfirmDialog.ShowAsync(
            this, "Confirm Delete", $"Are you sure you want to delete \"{name}\"?");
        msg.OnResult(response ?? false);
    }

#pragma warning restore VSTHRD100

}
