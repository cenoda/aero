using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aero.Views;

public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show a modal text-input dialog. Returns the entered text, or <c>null</c>
    /// if the user cancelled.
    /// </summary>
    public static Task<string?> ShowAsync(
        Window owner,
        string title,
        string prompt,
        string? defaultText = null)
    {
        var dialog = new TextInputDialog();
        dialog.Title = title;
        dialog.PromptText.Text = prompt;
        dialog.InputBox.Text = defaultText ?? string.Empty;
        dialog.InputBox.SelectAll();
        return dialog.ShowDialog<string?>(owner);
    }

    private void OnAcceptClicked(object? sender, RoutedEventArgs e) =>
        Close(InputBox.Text);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) =>
        Close(null);
}
