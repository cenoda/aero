using Aero.Languages;
using Aero.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aero.Views;

public partial class ProblemsView : UserControl
{
    public ProblemsView()
    {
        InitializeComponent();

        // Handle double-tap to navigate to diagnostic
        var listBox = this.FindControl<ListBox>("DiagnosticsList");
        if (listBox != null)
        {
            listBox.DoubleTapped += OnDiagnosticDoubleTapped;
        }
    }

    private void OnDiagnosticDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProblemsViewModel vm && sender is ListBox lb)
        {
            if (lb.SelectedItem is Diagnostic diagnostic)
            {
                vm.NavigateCommand.Execute(diagnostic);
            }
        }
    }
}
