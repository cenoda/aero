using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using Aero.ViewModels;

namespace Aero.Views;

public partial class FindReplaceOverlay : UserControl
{
    public FindReplaceOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is FindReplaceViewModel vm)
        {
            // When FocusReplaceOnOpen is set (Ctrl+H), focus the Replace TextBox
            vm.WhenAnyValue(x => x.FocusReplaceOnOpen)
              .Where(focus => focus)
              .Subscribe(_ =>
              {
                  Dispatcher.UIThread.Post(() =>
                  {
                      ReplaceTextBox.Focus();
                      vm.FocusReplaceOnOpen = false;
                  });
              });
        }
    }
}
