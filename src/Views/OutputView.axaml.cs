using System.Collections.Specialized;
using Avalonia.Controls;

namespace Aero.Views;

public partial class OutputView : UserControl
{
    public OutputView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Wire auto-scroll when Lines collection changes
        if (DataContext is ViewModels.OutputViewModel vm)
        {
            ((INotifyCollectionChanged)vm.Lines).CollectionChanged += OnLinesCollectionChanged;
        }
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom when new lines are added
        if (this.FindControl<ScrollViewer>("OutputScrollViewer") is { } scrollViewer)
        {
            scrollViewer.ScrollToEnd();
        }
    }
}