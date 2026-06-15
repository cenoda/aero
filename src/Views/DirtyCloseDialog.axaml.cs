using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aero.Core;

namespace Aero.Views;

public partial class DirtyCloseDialog : Window
{
    public DirtyCloseDialog()
    {
        InitializeComponent();
    }

    public static Task<string?> ShowAsync(Window owner, string fileName)
    {
        var dialog = new DirtyCloseDialog();
        dialog.MessageText.Text =
            $"You have unsaved changes in {fileName}.\n\n" +
            "Do you want to save them before closing?";
        return dialog.ShowDialog<string?>(owner);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e) =>
        Close(DirtyCloseResponse.Save);

    private void OnDontSaveClicked(object? sender, RoutedEventArgs e) =>
        Close(DirtyCloseResponse.DontSave);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) =>
        Close(DirtyCloseResponse.Cancel);
}
