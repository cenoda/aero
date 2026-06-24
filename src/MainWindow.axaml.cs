using System;
using System.Collections.Generic;
using Aero.Core;
using Aero.Docking;
using Aero.ViewModels;
using Aero.Views;
using Avalonia.Controls;
using Dock.Model.Controls;
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
    /// M1: Stub — real initialization moved to <see cref="AssignSpikeLayout"/>
    /// per T1.4 (plan §2.5 init sequence must run at layout-assignment time,
    /// not at app-start time before the template is applied).
    /// </summary>
    private void InitializeDockSpike()
    {
        // T1.4: Factory and Layout assignment deferred to AssignSpikeLayout().
        // The constructor-time call is kept as a hook point for future non-spike
        // dock initialization but does nothing for the spike path.
    }

    /// <summary>
    /// M1: Assign layout on first toggle, using the plan §2.5 init sequence
    /// (T1.4: Factory and layout init flags set from code-behind).
    /// Called from ShellViewModel when IsSpikeActive changes.
    ///
    /// T0.16/17: Idempotent — skips if a layout is already assigned.
    /// </summary>
    internal void AssignSpikeLayout()
    {
        // BUGFIX: Add file-based logging to diagnose toggle failure (GUI app)
        DebugLogger.Log("[Dock] AssignSpikeLayout CALLED");
        DebugLogger.Log($"[Dock]   DockSpikeControl is null: {DockSpikeControl == null}");

        if (DockSpikeControl == null) return;

        DebugLogger.Log($"[Dock]   DockSpikeControl.Layout before: {DockSpikeControl.Layout?.GetType().Name ?? "null"}");

        if (DockSpikeControl.Layout != null)
        {
            DebugLogger.Log("[Dock] AssignSpikeLayout: skipping — layout already assigned");
            return;
        }

        DebugLogger.Log("[Dock] AssignSpikeLayout: init sequence start");
        DebugLogger.Log(
            $"[Dock]   InitializeFactory (before): {DockSpikeControl.InitializeFactory}");

        // Plan §2.5: init flags BEFORE factory/layout assignment (T1.4 + T0.15)
        DockSpikeControl.InitializeFactory = true;
        DockSpikeControl.InitializeLayout = false;

        var layout = AeroDockFactory.CreateDefaultLayout();

        // M2: Wire real ViewModels into Context before assigning Layout.
        // This must happen BEFORE Layout is assigned so that DataTemplate resolution
        // picks up the Context immediately when the DockControl renders.
        if (DataContext is ShellViewModel shell)
        {
            WireViewModels(layout, shell);
        }
        else
        {
            DebugLogger.Log(
                "[Dock] AssignSpikeLayout: DataContext is not ShellViewModel — skipping WireViewModels");
        }

        // T1.4: use layout's factory as safety net (plan §2.5 step 4)
        DockSpikeControl.Factory = layout.Factory ?? AeroDockFactory.Factory;

        DebugLogger.Log(
            $"[Dock]   InitializeFactory (after): {DockSpikeControl.InitializeFactory}");
        DebugLogger.Log(
            $"[Dock]   Factory (after): {DockSpikeControl.Factory?.GetType().Name ?? "null"}");
        DebugLogger.Log(
            $"[Dock]   Layout type: {layout.GetType().Name}");

        DockSpikeControl.Layout = layout;

        DebugLogger.Log("[Dock] AssignSpikeLayout: layout assigned");
        if (DockSpikeControl.Layout is Dock.Model.Core.IDock root)
        {
            DebugLogger.Log(
                $"[Dock]   Root children: {root.VisibleDockables?.Count ?? 0}");
        }
        
        // BUGFIX: Check visibility after layout assignment
        DebugLogger.Log($"[Dock]   DockSpikeControl.IsVisible: {DockSpikeControl.IsVisible}");
        DebugLogger.Log($"[Dock]   DockSpikeControl.IsEffectivelyVisible: {DockSpikeControl.IsEffectivelyVisible}");
        DebugLogger.Log($"[Dock]   DockSpikeControl.IsInitialized: {DockSpikeControl.IsInitialized}");
        DebugLogger.Log($"[Dock]   DockSpikeControl.IsArrangeValid: {DockSpikeControl.IsArrangeValid}");
        DebugLogger.Log($"[Dock]   DockSpikeControl.IsMeasureValid: {DockSpikeControl.IsMeasureValid}");
        
        // BUGFIX: Force visual update
        DebugLogger.Log("[Dock]   Forcing visual update...");
        DockSpikeControl.InvalidateVisual();
        DockSpikeControl.InvalidateMeasure();
        DockSpikeControl.InvalidateArrange();
        DebugLogger.Log($"[Dock]   After invalidate - IsVisible: {DockSpikeControl.IsVisible}");
        DebugLogger.Log($"[Dock]   After invalidate - IsEffectivelyVisible: {DockSpikeControl.IsEffectivelyVisible}");
    }

    /// <summary>
    /// T0.18: Clear the dock layout when the spike is toggled off so the (invisible)
    /// DockControl does not keep an orphan IRootDock attached. This releases the
    /// factory's dockable references and allows GC of the spike tree.
    /// </summary>
    internal void ClearSpikeLayout()
    {
        // BUGFIX: Add file-based logging (GUI app)
        DebugLogger.Log("[Dock] ClearSpikeLayout CALLED");
        DebugLogger.Log($"[Dock]   DockSpikeControl is null: {DockSpikeControl == null}");

        if (DockSpikeControl == null) return;

        if (DockSpikeControl.Layout != null)
        {
            DebugLogger.Log(
                $"[Dock] ClearSpikeLayout: detaching {DockSpikeControl.Layout.GetType().Name}");
            DockSpikeControl.Layout = null;
            // BUGFIX: Clear InitializeFactory to ensure clean re-init
            DockSpikeControl.InitializeFactory = false;
            DockSpikeControl.InitializeLayout = false;
            // BUGFIX: Ensure focus returns to editor after toggling off
            // The DockControl might have focus even when invisible, blocking editor scroll
            if (DataContext is ViewModels.ShellViewModel shell)
            {
                shell.EditorViewModel.FocusEditor();
            }
        }
        else
        {
            DebugLogger.Log("[Dock] ClearSpikeLayout: nothing to clear");
        }
    }

    /// <summary>
    /// M2: Walk the dock layout tree and inject real ViewModels into each dockable's Context.
    /// Called after layout is built, before Layout is assigned to DockControl.
    /// This replaces the "M2-pending" placeholders set in AeroDockFactory (T1.1).
    /// </summary>
    private static void WireViewModels(IRootDock layout, ShellViewModel shell)
    {
        foreach (var dockable in EnumerateDockables(layout))
        {
            switch (dockable)
            {
                case Docking.ToolViewModels.ExplorerTool t:
                    t.Context = shell.FileExplorerViewModel;
                    break;
                case Docking.ToolViewModels.GitTool t:
                    t.Context = shell.GitViewModel;
                    break;
                case Docking.ToolViewModels.ProblemsTool t:
                    t.Context = shell.ProblemsViewModel;
                    break;
                case Docking.ToolViewModels.OutputTool t:
                    t.Context = shell.OutputViewModel;
                    break;
                case Docking.DocumentViewModels.EditorDocument d:
                    d.Context = shell.EditorViewModel;
                    break;
            }
            System.Diagnostics.Debug.WriteLine(
                $"[Dock] Wired {dockable.GetType().Name}.Context " +
                $"-> {dockable.Context?.GetType().Name ?? "null"}");
        }
    }

    /// <summary>
    /// M2: Recursively enumerate all IDockable instances in the layout tree.
    /// Walks VisibleDockables on each IDock encountered.
    /// </summary>
    private static IEnumerable<IDockable> EnumerateDockables(IDockable root)
    {
        yield return root;

        if (root is IDock dock && dock.VisibleDockables != null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                foreach (var descendant in EnumerateDockables(child))
                    yield return descendant;
            }
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

    /// <summary>
    /// BUGFIX: Control spike visibility in code-behind to bypass XAML binding issues.
    /// Also controls EditorView visibility (they are mutually exclusive).
    /// </summary>
    internal void SetSpikeVisibility(bool isVisible)
    {
        DebugLogger.Log($"[Dock] SetSpikeVisibility({isVisible})");
        
        // BUGFIX: Control the Border's visibility (it wraps DockSpikeControl)
        var dockBorder = this.FindControl<Border>("DockSpikeBorder");
        if (dockBorder != null)
        {
            // BUGFIX: Ensure layout is assigned BEFORE making visible
            if (isVisible && DockSpikeControl.Layout == null)
            {
                DebugLogger.Log("[Dock]   Layout is null, calling AssignSpikeLayout() first");
                AssignSpikeLayout();
            }
            
            dockBorder.IsVisible = isVisible;
            DebugLogger.Log($"[Dock]   DockSpikeBorder.IsVisible set to {isVisible}");
            DebugLogger.Log($"[Dock]   DockSpikeBorder.Bounds: {dockBorder.Bounds}");
            DebugLogger.Log($"[Dock]   DockSpikeControl.Layout: {DockSpikeControl?.Layout?.GetType().Name ?? "null"}");
        }
        else
        {
            DebugLogger.Log($"[Dock]   ERROR: DockSpikeBorder is null!");
        }
        
        // Find EditorView and toggle its visibility
        var editorView = this.FindControl<Views.EditorView>("EditorView");
        if (editorView != null)
        {
            editorView.IsVisible = !isVisible;
            DebugLogger.Log($"[Dock]   EditorView.IsVisible set to {!isVisible}");
        }
        else
        {
            DebugLogger.Log($"[Dock]   ERROR: EditorView not found by name!");
        }
    }
}
