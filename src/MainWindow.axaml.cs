using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Aero.Core;
using Aero.ViewModels;
using Aero.Views;

namespace Aero;

public partial class MainWindow : Window
{
    private IMessageBus? _bus;
    private Action<ConfirmDirtyClose>? _confirmDirtyCloseHandler;
    private bool _exitHandled;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
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
        _confirmDirtyCloseHandler = OnConfirmDirtyClose;
        _bus.Subscribe(_confirmDirtyCloseHandler);
    }

    /// <summary>
    /// Mark that the exit flow has already validated dirty documents (called by
    /// <see cref="ShellViewModel.ExitAsync"/> before <c>desktop.Shutdown()</c>).
    /// This prevents OnClosing from re-running the dirty-check prompt.
    /// </summary>
    internal void MarkExitHandled() => _exitHandled = true;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // If ExitAsync() already handled the dirty check, just clean up
        if (_exitHandled)
        {
            UnsubscribeBus();
            return;
        }

        // Check dirty documents before allowing OS window close (e.g. clicking "X")
        if (DataContext is ShellViewModel shell)
        {
            e.Cancel = true;
            var canExit = await shell.CheckDirtyBeforeExitAsync();
            if (canExit)
            {
                _exitHandled = true;
                Close();
            }
        }
    }

    private void UnsubscribeBus()
    {
        if (_bus != null && _confirmDirtyCloseHandler != null)
        {
            _bus.Unsubscribe<ConfirmDirtyClose>(_confirmDirtyCloseHandler);
            _confirmDirtyCloseHandler = null;
        }
    }

    private async void OnConfirmDirtyClose(ConfirmDirtyClose msg)
    {
        var result = await DirtyCloseDialog.ShowAsync(this, msg.FileName);
        msg.OnResponse(result ?? DirtyCloseResponse.Cancel);
    }
}
