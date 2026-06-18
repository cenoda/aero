using Avalonia.Controls;

namespace Aero.Views;

/// <summary>
/// View-only code-behind for the file explorer sidebar. Pure XAML binding
/// surface today; future M3+ wiring (file activation, context menu) will
/// route through ViewModels via MessageBus, not direct calls.
/// </summary>
public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
    }
}
