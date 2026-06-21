using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aero.Terminal;
using Xunit;

namespace Aero.Tests.Terminal;

public class ProcessRunnerTests
{
    // -------------------------------------------------------------------
    // Happy path — run dotnet --version
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_DotnetVersion_ReturnsExitCodeZero()
    {
        var runner = new ProcessRunner();
        var lines = new List<string>();

        var exitCode = await runner.RunAsync(
            "dotnet",
            "--version",
            null,
            line => lines.Add(line),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(lines);
    }

    // -------------------------------------------------------------------
    // Cancel — start a long-running command and cancel
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CancelLongRunningCommand_DoesNotThrow()
    {
        var runner = new ProcessRunner();
        var lines = new List<string>();
        using var cts = new CancellationTokenSource();

        // Use a cross-platform long-running command.
        // Linux/macOS: sleep 10; Windows: timeout /t 10 /nobreak
        var (executable, arguments) = System.Runtime.InteropServices
            .RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows)
            ? ("timeout", "/t 10 /nobreak")
            : ("sleep", "10");

        // Cancel after 100ms — the process should still be running
        cts.CancelAfter(100);

        var exitCode = await runner.RunAsync(
            executable,
            arguments,
            null,
            line => lines.Add(line),
            cts.Token);

        // Should return -1 without throwing (cancelled before process exits)
        Assert.Equal(-1, exitCode);
    }

    // -------------------------------------------------------------------
    // Bad binary — call with non-existent executable
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_BadBinary_ReturnsMinusOne()
    {
        var runner = new ProcessRunner();
        var lines = new List<string>();

        var exitCode = await runner.RunAsync(
            "__nonexistent_binary__",
            "",
            null,
            line => lines.Add(line),
            CancellationToken.None);

        Assert.Equal(-1, exitCode);
        Assert.NotEmpty(lines);
    }
}
