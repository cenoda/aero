using Avalonia.Controls;
using Avalonia.Interactivity;
using Aero.ViewModels;

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
        }
    }

    private void OnTreeContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem item)
        {
            item.Expanded -= OnItemExpanded;
            item.Expanded += OnItemExpanded;
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
