using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Aero.Core;
using Aero.Languages;
using Aero.Terminal;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the Output panel. Manages command execution, output lines,
/// and UI state for the terminal emulation.
/// </summary>
public class OutputViewModel : ReactiveObject, IDisposable
{
    private readonly IProcessRunner _processRunner;
    private readonly IMessageBus _bus;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _wasCancelled;

    // Constants for terminal-state lines (per TOFIX R1.10)
    private const string ExitLineFmt = "[Process exited with code {0}]";
    private const string CancelledLine = "[Cancelled]";
    private const int MaxLines = 10_000;

    [Reactive] public string CommandText { get; set; } = "";
    [Reactive] public string WorkingDirectory { get; set; } = "";
    [Reactive] public bool IsRunning { get; set; }

    public ObservableCollection<string> Lines { get; } = new();

    // Commands
    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    // Stored handler for unsubscribe
    private Action<FolderOpened>? _folderOpenedHandler;

    public OutputViewModel(IProcessRunner processRunner, IMessageBus bus)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        // Initialize commands with CanExecute guards
        var canRun = this.WhenAnyValue(x => x.IsRunning)
            .CombineLatest(this.WhenAnyValue(x => x.CommandText), (running, text) =>
                !running && !string.IsNullOrWhiteSpace(text))
            .Select(x => x);

        var canCancel = this.WhenAnyValue(x => x.IsRunning);

        RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);
        CancelCommand = ReactiveCommand.Create(Cancel, canCancel);
        ClearCommand = ReactiveCommand.Create(Clear);

        // Subscribe to FolderOpened to keep WorkingDirectory current
        _folderOpenedHandler = msg => WorkingDirectory = msg.Path;
        _bus.Subscribe(_folderOpenedHandler);
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandText))
            return;

        IsRunning = true;
        _wasCancelled = false;
        _cts = new CancellationTokenSource();

        try
        {
            // Parse command - simple split on first space for executable/arguments
            var parts = CommandText.TrimStart().Split(' ', 2);
            var executable = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : "";

            var exitCode = await _processRunner.RunAsync(
                executable,
                arguments,
                string.IsNullOrEmpty(WorkingDirectory) ? null : WorkingDirectory,
                AppendLine,
                _cts.Token);

            // Append terminal-state line (per TOFIX R2.1)
            // OutputViewModel tracks _wasCancelled instead of relying on ProcessRunner
            if (_wasCancelled)
            {
                AppendLine(CancelledLine);
            }
            else
            {
                AppendLine(string.Format(ExitLineFmt, exitCode));
            }
        }
        catch (Exception ex)
        {
            // Should not reach here - ProcessRunner catches all exceptions
            AppendLine($"[Error: {ex.Message}]");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
        }
    }

    private void Cancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _wasCancelled = true;
            _cts.Cancel();
        }
    }

    /// <summary>
    /// Run an external command programmatically. Unlike RunCommand, this does not
    /// parse CommandText - takes explicit executable/arguments.
    /// Used by ShellViewModel.BuildCommand.
    /// </summary>
    public async Task RunExternalAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new ArgumentNullException(nameof(executable));

        IsRunning = true;
        _wasCancelled = false;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var exitCode = await _processRunner.RunAsync(
                executable,
                arguments,
                workingDirectory,
                AppendLine,
                _cts.Token);

            if (_wasCancelled)
            {
                AppendLine(CancelledLine);
            }
            else
            {
                AppendLine(string.Format(ExitLineFmt, exitCode));
            }
        }
        catch (Exception ex)
        {
            AppendLine($"[Error: {ex.Message}]");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
        }
    }

    private void Clear()
    {
        Lines.Clear();
    }

    /// <summary>
    /// Append a line to the output collection, marshaling to the UI thread
    /// if necessary (per TOFIX R1.2).
    /// </summary>
    private void AppendLine(string line)
    {
        var dispatcher = GetUiDispatcher();
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => AppendLineInternal(line));
        }
        else
        {
            AppendLineInternal(line);
        }
    }

    private void AppendLineInternal(string line)
    {
        if (_disposed)
            return;

        Lines.Add(line);

        // Enforce line cap (per TOFIX R1.10)
        while (Lines.Count > MaxLines)
        {
            Lines.RemoveAt(0);
        }
    }

    /// <summary>
    /// Return the Avalonia UI-thread dispatcher when running inside the app.
    /// Returns null in unit tests (no Avalonia application).
    /// </summary>
    private static Dispatcher? GetUiDispatcher()
    {
        if (Avalonia.Application.Current == null)
            return null;
        try
        {
            return Dispatcher.UIThread;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Cancel any running command
        _cts?.Cancel();
        _cts?.Dispose();

        // Unsubscribe from message bus
        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
    }
}