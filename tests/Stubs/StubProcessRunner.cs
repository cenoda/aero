using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aero.Terminal;

namespace Aero.Tests.Stubs;

/// <summary>
/// A configurable stub implementation of <see cref="IProcessRunner"/>
/// for testing <see cref="Aero.ViewModels.OutputViewModel"/> without
/// running real OS processes.
/// </summary>
public class StubProcessRunner : IProcessRunner
{
    /// <summary>Exit code returned by <see cref="RunAsync"/>.</summary>
    public int ExitCode { get; set; } = 0;

    /// <summary>
    /// Lines emitted via the <c>onLine</c> delegate during <see cref="RunAsync"/>.
    /// Each entry is emitted in order.
    /// </summary>
    public List<string> EmittedLines { get; set; } = new();

    /// <summary>
    /// If <c>true</c>, <see cref="RunAsync"/> simulates a long-running
    /// operation that observes the <c>cancellationToken</c> and returns -1
    /// when cancelled.  Lines in <see cref="EmittedLines"/> are emitted
    /// synchronously before entering the delay loop.
    /// </summary>
    public bool SimulateLongRunning { get; set; }

    /// <summary>
    /// If non-null, <see cref="RunAsync"/> simulates a startup exception
    /// by emitting <c>"[Error: {SimulateStartupException.Message}]"</c>
    /// on <c>onLine</c> and immediately returning -1.
    /// </summary>
    public Exception? SimulateStartupException { get; set; }

    /// <summary>
    /// Set to <c>true</c> after <see cref="RunAsync"/> has returned.
    /// </summary>
    public bool WasCalled { get; private set; }

    /// <summary>Captured working directory argument from the last call.</summary>
    public string? CapturedWorkingDirectory { get; private set; }

    public async Task<int> RunAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        CapturedWorkingDirectory = workingDirectory;
        onLine = onLine ?? (_ => { });

        // Startup-exception simulation
        if (SimulateStartupException != null)
        {
            onLine($"[Error: {SimulateStartupException.Message}]");
            return -1;
        }

        // Emit pre-configured lines synchronously
        foreach (var line in EmittedLines)
        {
            onLine(line);
        }

        // Long-running simulation — observe cancellation
        if (SimulateLongRunning)
        {
            try
            {
                // Yield once so the caller can set up cancellation
                await Task.Yield();
                // Delay long enough that cancellation will be observed
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return -1;
            }
        }

        return ExitCode;
    }
}