using Avalonia.Controls;
using Avalonia.Input;
using Aero.ViewModels;

namespace Aero;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Handle key input for shortcuts not handled by menu
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Additional keyboard shortcuts can be handled here
    }
}
