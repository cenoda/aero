using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using AeroCore = Aero.Core;

namespace Aero.ViewModels;

/// <summary>
/// Top-level ViewModel for the main application window.
/// Owns the overall layout state: active panel, status bar text, etc.
/// </summary>
public class ShellViewModel : ReactiveObject
{
    private readonly AeroCore.IMessageBus _bus;

    [Reactive] public string StatusText { get; set; } = "Aero IDE";

    public ShellViewModel(AeroCore.IMessageBus bus)
    {
        _bus = bus;

        _bus.Subscribe<AeroCore.FolderOpened>(msg => StatusText = msg.Path);
        _bus.Subscribe<AeroCore.ActiveDocumentChanged>(msg =>
            StatusText = msg.FilePath ?? "Aero IDE");
    }
}
