using System;
using Aero.Core;
using Aero.Docking;
using Aero.ViewModels;
using Aero.Views;
using Avalonia.Controls;
using Dock.Model.Core;

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

        // M0.5: Initialize the Dock spike control with C#-created layout
        InitializeDockSpike();
    }

    /// <summary>
    /// M1: Initialize the Dock spike control with AeroDockFactory.
    /// Assigns only the factory; layout is deferred to user toggle.
    /// </summary>
    private void InitializeDockSpike()
    {
        if (DockSpikeControl == null) return;

        DockSpikeControl.Factory = AeroDockFactory.Factory;
        System.Diagnostics.Debug.WriteLine($"[Dock] M1 Factory assigned: {DockSpikeControl.Factory?.GetType().Name ?? "null"}");
    }

    /// <summary>
    /// M0.5: Assign layout on first toggle (Issue 9: after template is applied).
    /// Called from ShellViewModel when IsSpikeActive changes.
    ///
    /// Issue T0.16/17: Idempotent — skips if a layout is already assigned.
    /// Repeated toggles on/off no longer accumulate orphaned layouts.
    /// </summary>
    internal void AssignSpikeLayout()
    {
        if (DockSpikeControl == null) return;

        // T0.16/17: Idempotent guard. If a layout is already assigned, the existing
        // tree is reused. Rapid double-toggles no longer stack new IRootDock instances.
        if (DockSpikeControl.Layout != null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Dock] AssignSpikeLayout: skipping — layout already assigned");
            return;
        }

        // Issue 4: Log DockControl state before assignment
        System.Diagnostics.Debug.WriteLine($"[Dock] Before layout assign:");
        System.Diagnostics.Debug.WriteLine($"[Dock]   Factory: {DockSpikeControl.Factory?.GetType().Name ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"[Dock]   Layout pre: {DockSpikeControl.Layout?.GetType().Name ?? "null"}");

        var layout = AeroDockFactory.CreateDefaultLayout();
        DockSpikeControl.Layout = layout;

        // Issue 4: Log DockControl state after assignment
        System.Diagnostics.Debug.WriteLine($"[Dock] After layout assign:");
        System.Diagnostics.Debug.WriteLine($"[Dock]   Layout post: {DockSpikeControl.Layout?.GetType().Name ?? "null"}");
        if (DockSpikeControl.Layout is Dock.Model.Core.IDock root)
        {
            System.Diagnostics.Debug.WriteLine($"[Dock]   Root children: {root.VisibleDockables?.Count ?? 0}");
        }
    }

    /// <summary>
    /// T0.18: Clear the dock layout when the spike is toggled off so the (invisible)
    /// DockControl does not keep an orphan IRootDock attached. This releases the
    /// factory's dockable references and allows GC of the spike tree.
    /// </summary>
    internal void ClearSpikeLayout()
    {
        if (DockSpikeControl == null) return;

        if (DockSpikeControl.Layout != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Dock] ClearSpikeLayout: detaching {DockSpikeControl.Layout.GetType().Name}");
            DockSpikeControl.Layout = null;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[Dock] ClearSpikeLayout: nothing to clear");
        }
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
