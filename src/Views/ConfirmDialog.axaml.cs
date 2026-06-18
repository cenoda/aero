using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aero.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show a modal confirmation dialog. Returns <c>true</c> (Yes),
    /// <c>false</c> (No), or <c>null</c> (cancelled / window closed).
    /// </summary>
    public static Task<bool?> ShowAsync(
        Window owner,
        string title,
        string message)
    {
        var dialog = new ConfirmDialog();
        dialog.Title = title;
        dialog.MessageText.Text = message;
        return dialog.ShowDialog<bool?>(owner);
    }

    private void OnYesClicked(object? sender, RoutedEventArgs e) =>
        Close(true);

    private void OnNoClicked(object? sender, RoutedEventArgs e) =>
        Close(false);
}
