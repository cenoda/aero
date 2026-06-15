using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Aero.ViewModels;

namespace Aero.Views;

public partial class FindReplaceOverlay : UserControl
{
    public FindReplaceOverlay()
    {
        InitializeComponent();
        this.GetObservable(IsVisibleProperty).Subscribe(OnIsVisibleChanged);
    }

    /// <summary>
    /// When the overlay becomes visible, check if we should focus the Replace field (Ctrl+H).
    /// Reacting here ensures the control is in the visual tree and can receive focus.
    /// </summary>
    private void OnIsVisibleChanged(bool isVisible)
    {
        if (!isVisible)
            return;

        if (DataContext is not FindReplaceViewModel vm)
            return;

        if (vm.FocusReplaceOnOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ReplaceTextBox.Focus();
                vm.FocusReplaceOnOpen = false;
            });
        }
        else
        {
            Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
        }
    }
}
