using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Aero.Core;
using Aero.Languages;
using ReactiveUI;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the Problems panel, showing all current diagnostics
/// in the workspace.
/// </summary>
public class ProblemsViewModel : ReactiveObject, IDisposable
{
    private readonly Aero.Core.IMessageBus _bus;
    private readonly Action<DiagnosticsUpdated>? _handler;
    private bool _disposed;

    private ObservableCollection<Diagnostic> _diagnostics = new();
    private bool _isVisible;
    private ICommand? _navigateCommand;

    public ProblemsViewModel(Aero.Core.IMessageBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        // Subscribe to DiagnosticsUpdated message from the bus (R2.7 - drop duplicate event subscription)
        _handler = OnDiagnosticsUpdated;
        _bus.Subscribe(_handler);

        NavigateCommand = ReactiveCommand.Create<Diagnostic>(NavigateToDiagnostic);
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

    public ICommand NavigateCommand
    {
        get => _navigateCommand ?? throw new InvalidOperationException("NavigateCommand not initialized");
        private set => this.RaiseAndSetIfChanged(ref _navigateCommand, value);
    }

    private void NavigateToDiagnostic(Diagnostic diagnostic)
    {
        // Publish NavigateToLocation message to open the file and navigate to the line
        _bus.Publish(new NavigateToLocation(diagnostic.FileUri, diagnostic.Range.StartLine, diagnostic.Range.StartCharacter));
    }

    private void OnDiagnosticsUpdated(DiagnosticsUpdated message)
    {
        // Update on UI thread — DiagnosticsUpdated may be published from a background
        // thread (e.g. DiagnosticStore.PublishDiagnosticsUpdated). Marshal to UI thread
        // to avoid cross-thread collection access on ObservableCollection.
        var dispatcher = GetUiDispatcher();
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => UpdateDiagnostics(message.Diagnostics));
        }
        else
        {
            UpdateDiagnostics(message.Diagnostics);
        }
    }

    private static Avalonia.Threading.Dispatcher? GetUiDispatcher()
    {
        try
        {
            return Avalonia.Threading.Dispatcher.UIThread;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void UpdateDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        Diagnostics = new ObservableCollection<Diagnostic>(
            diagnostics.OrderBy(d => d.FileUri).ThenBy(d => d.Range.StartLine));
    }

    /// <summary>
    /// Dispose message bus subscription to prevent stale-handler leaks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_handler != null)
            _bus.Unsubscribe<DiagnosticsUpdated>(_handler);
    }
}
