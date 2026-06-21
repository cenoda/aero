using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Aero.Core;
using Aero.Languages;
using ReactiveUI;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the Problems panel, showing all current diagnostics
/// in the workspace.
/// </summary>
public class ProblemsViewModel : ReactiveObject
{
    private readonly Aero.Core.IMessageBus _bus;
    private readonly Action<DiagnosticsUpdated>? _handler;

    private ObservableCollection<Diagnostic> _diagnostics = new();
    private bool _isVisible;

    public ProblemsViewModel(Aero.Core.IMessageBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        _handler = OnDiagnosticsUpdated;
        _bus.Subscribe(_handler);
    }

    public ObservableCollection<Diagnostic> Diagnostics
    {
        get => _diagnostics;
        private set => this.RaiseAndSetIfChanged(ref _diagnostics, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    private void OnDiagnosticsUpdated(DiagnosticsUpdated message)
    {
        // Update on UI thread
        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => UpdateDiagnostics(message.Diagnostics));
        }
        else
        {
            UpdateDiagnostics(message.Diagnostics);
        }
    }

    private void UpdateDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        Diagnostics = new ObservableCollection<Diagnostic>(
            diagnostics.OrderBy(d => d.FileUri).ThenBy(d => d.Range.StartLine));
    }
}