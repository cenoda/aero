using System;
using Avalonia.Controls;
using Avalonia.Input;
using Aero.Core;
using Aero.Views;

namespace Aero;

public partial class MainWindow : Window
{
    private IMessageBus? _bus;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Wire the bus subscription after construction. Avalonia's XAML loader requires
    /// a parameterless constructor, so DI of <see cref="IMessageBus"/> happens via
    /// this explicit initializer called from <c>App.axaml.cs</c>.
    /// </summary>
    public void Initialize(IMessageBus bus)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        _bus = bus;
        _bus.Subscribe<ConfirmDirtyClose>(OnConfirmDirtyClose);
    }

    private async void OnConfirmDirtyClose(ConfirmDirtyClose msg)
    {
        var result = await DirtyCloseDialog.ShowAsync(this, msg.FileName);
        msg.OnResponse(result ?? DirtyCloseResponse.Cancel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Additional keyboard shortcuts can be handled here
    }
}
