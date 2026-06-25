using System.Reactive.Linq;
using Aero.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aero.Views;

/// <summary>
/// View code-behind for the file explorer sidebar. The XAML owns all bindings;
/// this code-behind only wires the lazy-load trigger: each <c>TreeViewItem</c>
/// created by the <see cref="TreeView"/> subscribes to its <c>Expanded</c>
/// event so a directory node asks the VM to populate its children the first
/// time the user opens it.
/// </summary>
public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // ContainerPrepared fires once per TreeViewItem created by the TreeView,
        // including items created when the user expands a node and its children
        // become visible. By hooking Expanded on each, we catch both first-time
        // load (the root item) and subsequent expansions.
        if (this.FindControl<TreeView>("ExplorerTree") is { } tree)
        {
            tree.ContainerPrepared += OnTreeContainerPrepared;
            tree.KeyDown += OnTreeKeyDown;
            tree.DoubleTapped += OnTreeDoubleTapped;
        }
    }

    private void OnTreeContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem item)
        {
            item.Expanded -= OnItemExpanded;
            item.Expanded += OnItemExpanded;

            // ISSUE-012: ContainerPrepared on the root TreeView only fires for
            // direct TreeViewItems. Nested TreeViewItems (level 2+) are created
            // by TreeDataTemplate inside each TreeViewItem, so their
            // ContainerPrepared events fire on the PARENT TreeViewItem, not on
            // the root TreeView. Recursively subscribe so every nesting level
            // gets the Expanded handler wired.
            item.ContainerPrepared -= OnTreeContainerPrepared;
            item.ContainerPrepared += OnTreeContainerPrepared;
        }
    }

    private async void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        // async void allowed here: this is an Avalonia event handler.
        if (DataContext is not FileExplorerViewModel vm) return;

        // Walk up the visual tree from the source element to find the
        // TreeViewItem that was double-clicked. This works for both root
        // items and lazily-loaded child items without per-container hooks.
        var source = e.Source as Avalonia.Visual;
        while (source != null && source is not TreeViewItem)
        {
            source = Avalonia.VisualTree.VisualExtensions.GetVisualParent(source);
        }

        if (source is not TreeViewItem item) return;
        if (item.DataContext is not FileExplorerNodeViewModel node) return;

        // Directories expand/collapse via the TreeView's default behavior.
        // Only file nodes trigger editor activation.
        if (!node.IsDirectory)
        {
            e.Handled = true;
            await vm.OpenFileAsync(node);
        }
    }

    private async void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        // async void allowed here: this is an Avalonia event handler.
        if (DataContext is not FileExplorerViewModel vm) return;
        var selected = vm.SelectedNode;
        if (selected == null) return;

        switch (e.Key)
        {
            case Key.Enter:
                if (selected.IsDirectory)
                {
                    // Enter on a directory toggles expansion.
                    selected.IsExpanded = !selected.IsExpanded;
                }
                else
                {
                    await vm.OpenSelectedFileAsync();
                }
                e.Handled = true;
                break;

            case Key.F2:
                await vm.RenameCommand.Execute(selected).FirstAsync();
                e.Handled = true;
                break;

            case Key.Delete:
                await vm.DeleteCommand.Execute(selected).FirstAsync();
                e.Handled = true;
                break;
        }
    }

    private async void OnItemExpanded(object? sender, RoutedEventArgs e)
    {
        // async void allowed here: this is an Avalonia event handler.
        // Exceptions inside EnsureChildrenLoadedAsync are swallowed by the VM
        // (it keeps the placeholder so the user can retry).
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeViewItem item) return;
        if (item.DataContext is not FileExplorerNodeViewModel node) return;

        await vm.EnsureChildrenLoadedAsync(node);
    }
}
