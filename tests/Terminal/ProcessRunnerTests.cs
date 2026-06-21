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

        // Start a command that will run for a while, then cancel after 100ms
        cts.CancelAfter(100);

        var exitCode = await runner.RunAsync(
            "sleep",
            "10",
            null,
            line => lines.Add(line),
            cts.Token);

        // Should return without throwing
        Assert.True(true);
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