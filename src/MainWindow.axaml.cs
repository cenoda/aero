using System;
using Aero.Core;
using Aero.Docking;
using Aero.ViewModels;
using Aero.Views;
using Avalonia.Controls;

namespace Aero;

public partial class MainWindow : Window
{
    private IMessageBus? _bus;
    private Action<ConfirmDirtyClose>? _confirmDirtyCloseHandler;
    private Action<PromptNewItem>? _promptNewItemHandler;
    private Action<PromptRename>? _promptRenameHandler;
    private Action<ConfirmDelete>? _confirmDeleteHandler;
    private bool _exitHandled;

    public MainWindow()
    {
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

        // Initialize DockControl with the default layout
        InitializeDockControl();
    }

    private void InitializeDockControl()
    {
        if (DockControl == null) return;

        // Create the factory and layout
        // CreateDefaultLayout() creates its own factory and calls InitLayout(root)
        var layout = AeroDockFactory.CreateDefaultLayout();

        // CRITICAL: Set InitializeFactory BEFORE Layout assignment.
        // When Layout is set, DockControl.OnPropertyChanged fires Initialize()
        // which checks InitializeFactory to set up locators (ContextLocator,
        // HostWindowLocator, etc.) that drag-and-drop needs.
        DockControl.InitializeFactory = true;

        // We already called InitLayout inside CreateDefaultLayout, so prevent double-init.
        DockControl.InitializeLayout = false;

        // Set the factory explicitly as a safety net for drag-and-drop wiring.
        // The factory from the layout tree is the same AeroDockFactory instance,
        // but DockControl needs it on its own property for some internal lookups.
        DockControl.Factory = layout.Factory!;

        // Now assign layout — this triggers Initialize() with all locators ready.
        DockControl.Layout = layout;
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
    }

    /// <summary>
    /// Mark that the exit flow has already validated dirty documents (called by
    /// <see cref="ShellViewModel.ExitAsync"/> before <c>desktop.Shutdown()</c>).
    /// This prevents OnClosing from re-running the dirty-check prompt.
    /// </summary>
    internal void MarkExitHandled() => _exitHandled = true;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
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
