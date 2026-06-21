using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Terminal;
using Aero.Tests.Stubs;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Headless unit tests for <see cref="OutputViewModel"/>.
/// These tests use <see cref="StubProcessRunner"/> instead of a real
/// process and do not require an Avalonia UI thread (the guarded-dispatcher
/// pattern in OutputViewModel returns <c>null</c> when
/// <c>Avalonia.Application.Current</c> is <c>null</c>, so all line appends
/// happen synchronously on the test thread).
/// </summary>
public class OutputViewModelTests
{
    // -------------------------------------------------------------------
    // Factory helpers
    // -------------------------------------------------------------------

    private static (OutputViewModel vm, StubProcessRunner runner, StubMessageBus bus) Create()
    {
        var runner = new StubProcessRunner();
        var bus = new StubMessageBus();
        var vm = new OutputViewModel(runner, bus);
        return (vm, runner, bus);
    }

    /// <summary>Await the completion of a ReactiveCommand{Unit, Unit} execution.</summary>
    private static async Task RunCommandAsync(OutputViewModel vm)
    {
        await vm.RunCommand.Execute().FirstAsync();
    }

    /// <summary>Await the completion of CancelCommand execution.</summary>
    private static async Task CancelCommandAsync(OutputViewModel vm)
    {
        await vm.CancelCommand.Execute().FirstAsync();
    }

    // -------------------------------------------------------------------
    // Command CanExecute guards
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_CannotExecute_WhenCommandTextIsEmpty()
    {
        var (vm, _, _) = Create();
        vm.CommandText = "";

        var canExecute = await vm.RunCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);
    }

    [Fact]
    public async Task RunCommand_CannotExecute_WhenIsRunning()
    {
        var (vm, runner, _) = Create();
        vm.CommandText = "dotnet --version";
        runner.SimulateLongRunning = true;

        // Initially should be executable
        var canExecuteBefore = await vm.RunCommand.CanExecute.FirstAsync();
        Assert.True(canExecuteBefore);

        // Start running and check CanExecute while it's running
        var runTask = RunCommandAsync(vm);

        var canExecuteDuring = await vm.RunCommand.CanExecute.FirstAsync();
        Assert.False(canExecuteDuring);

        // Cancel so the long-running stub returns
        await CancelCommandAsync(vm);
        await runTask;
    }

    [Fact]
    public async Task CancelCommand_CannotExecute_WhenNotRunning()
    {
        var (vm, _, _) = Create();

        var canExecute = await vm.CancelCommand.CanExecute.FirstAsync();
        Assert.False(canExecute);

        vm.CommandText = "dotnet --version";
        await RunCommandAsync(vm);

        // After the command completes synchronously, IsRunning should be false
        var canExecuteAfter = await vm.CancelCommand.CanExecute.FirstAsync();
        Assert.False(canExecuteAfter);
    }

    // -------------------------------------------------------------------
    // ClearCommand
    // -------------------------------------------------------------------

    [Fact]
    public async Task ClearCommand_EmptiesLines()
    {
        var (vm, runner, _) = Create();
        runner.EmittedLines.Add("line 1");
        runner.EmittedLines.Add("line 2");
        vm.CommandText = "some command";

        await RunCommandAsync(vm);

        // Verify lines were added
        Assert.Contains("line 1", vm.Lines);
        Assert.Contains("line 2", vm.Lines);

        // Clear and verify
        vm.ClearCommand.Execute().Subscribe();
        Assert.Empty(vm.Lines);
    }

    // -------------------------------------------------------------------
    // IsRunning lifecycle
    // -------------------------------------------------------------------

    [Fact]
    public async Task IsRunning_FalseAfterCommandCompletes()
    {
        var (vm, runner, _) = Create();
        runner.EmittedLines.Add("hello");
        vm.CommandText = "echo hello";

        await RunCommandAsync(vm);

        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task RunCommand_ProducesOutput()
    {
        var (vm, runner, _) = Create();
        runner.EmittedLines.Add("hello");
        vm.CommandText = "echo hello";

        await RunCommandAsync(vm);

        Assert.Contains("hello", vm.Lines);
    }

    // -------------------------------------------------------------------
    // Exit code line on success
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_AppendsExitCodeLine_OnSuccess()
    {
        var (vm, runner, _) = Create();
        runner.ExitCode = 0;
        runner.EmittedLines.Add("output");
        vm.CommandText = "dotnet --version";

        await RunCommandAsync(vm);

        Assert.Contains("[Process exited with code 0]", vm.Lines);
    }

    [Fact]
    public async Task RunCommand_AppendsExitCodeLine_WithNonZeroExit()
    {
        var (vm, runner, _) = Create();
        runner.ExitCode = 42;
        vm.CommandText = "failing command";

        await RunCommandAsync(vm);

        Assert.Contains("[Process exited with code 42]", vm.Lines);
    }

    // -------------------------------------------------------------------
    // Cancelled line on cancel
    // -------------------------------------------------------------------

    [Fact]
    public async Task CancelCommand_AppendsCancelledLine()
    {
        var (vm, runner, _) = Create();
        runner.SimulateLongRunning = true;
        runner.EmittedLines.Add("before cancel");
        vm.CommandText = "long command";

        // Start the long-running command (will yield at Task.Yield())
        var runTask = RunCommandAsync(vm);

        // It should be running now (after yield gives control back)
        Assert.True(vm.IsRunning);

        // Cancel — sets _wasCancelled, then cancels the CTS
        await CancelCommandAsync(vm);

        // Wait for the run to finish
        await runTask;

        // After completion, should show cancelled line but not exit-code line
        Assert.Contains("[Cancelled]", vm.Lines);
        Assert.DoesNotContain("[Process exited with code", vm.Lines);
        Assert.False(vm.IsRunning);
    }

    // -------------------------------------------------------------------
    // WorkingDirectory updated from FolderOpened
    // -------------------------------------------------------------------

    [Fact]
    public void FolderOpened_UpdatesWorkingDirectory()
    {
        var (vm, _, bus) = Create();

        Assert.Equal("", vm.WorkingDirectory);

        bus.Publish(new FolderOpened("/workspace/my-project"));

        Assert.Equal("/workspace/my-project", vm.WorkingDirectory);
    }

    [Fact]
    public async Task RunCommand_PassesWorkingDirectoryToProcessRunner()
    {
        var (vm, runner, bus) = Create();
        bus.Publish(new FolderOpened("/workspace/test"));
        vm.CommandText = "dotnet build";
        runner.ExitCode = 0;

        await RunCommandAsync(vm);

        Assert.Equal("/workspace/test", runner.CapturedWorkingDirectory);
    }

    // -------------------------------------------------------------------
    // Line cap enforcement
    // -------------------------------------------------------------------

    [Fact]
    public async Task Lines_DoesNotExceedMaxLines()
    {
        var (vm, runner, _) = Create();
        // Emit more than MaxLines
        var totalLines = 10_050;
        for (int i = 0; i < totalLines; i++)
        {
            runner.EmittedLines.Add($"line {i}");
        }
        vm.CommandText = "verbose command";

        await RunCommandAsync(vm);

        // Snapshot the line count after execution completes
        var lineCount = vm.Lines.Count;

        // MaxLines = 10_000, plus the exit-code line = 10_001 max
        Assert.True(lineCount <= 10_001,
            $"Expected at most 10_001 lines, got {lineCount}");

        // The last lines should be the most recent ones (FIFO eviction)
        var lines = vm.Lines.ToArray(); // snapshot to avoid enumeration issues
        Assert.Contains("line 9999", lines);
        Assert.Contains("[Process exited with code 0]", lines);
    }

    // -------------------------------------------------------------------
    // Startup-error path
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_WhenProcessFailsToStart_AppendsErrorAndExitCode()
    {
        var (vm, runner, _) = Create();
        runner.SimulateStartupException = new System.ComponentModel.Win32Exception("File not found");
        // Do not set SimulateLongRunning or EmittedLines
        vm.CommandText = "__nonexistent__";

        await RunCommandAsync(vm);

        // Should have an error line from the runner + exit-code line from VM
        Assert.Contains(vm.Lines, l => l.StartsWith("[Error:"));
        Assert.Contains("[Process exited with code -1]", vm.Lines);
        Assert.False(vm.IsRunning);
    }

    // -------------------------------------------------------------------
    // Multiple runs accumulate lines
    // -------------------------------------------------------------------

    [Fact]
    public async Task MultipleRuns_AccumulateLines()
    {
        var (vm, runner, _) = Create();
        runner.ExitCode = 0;

        vm.CommandText = "first";
        runner.EmittedLines.Clear();
        runner.EmittedLines.Add("run 1 output");
        await RunCommandAsync(vm);

        vm.CommandText = "second";
        runner.EmittedLines.Clear();
        runner.EmittedLines.Add("run 2 output");
        await RunCommandAsync(vm);

        Assert.Contains("run 1 output", vm.Lines);
        Assert.Contains("run 2 output", vm.Lines);
        Assert.Equal(2, vm.Lines.Count(l => l.StartsWith("[Process exited with code 0]")));
    }

    // -------------------------------------------------------------------
    // Dispose unsubscribes from FolderOpened
    // -------------------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromFolderOpened()
    {
        var (vm, _, bus) = Create();
        vm.Dispose();

        // After dispose, FolderOpened should not update WorkingDirectory
        bus.Publish(new FolderOpened("/should-not-update"));
        Assert.Equal("", vm.WorkingDirectory);
    }

    // -------------------------------------------------------------------
    // RunCommand with whitespace command is a no-op
    // -------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_WithWhitespaceCommand_DoesNotRun()
    {
        var (vm, runner, _) = Create();
        vm.CommandText = "   ";

        await RunCommandAsync(vm);

        Assert.False(runner.WasCalled);
        Assert.Empty(vm.Lines);
    }

    // -------------------------------------------------------------------
    // Cancel while not running is safe
    // -------------------------------------------------------------------

    [Fact]
    public async Task CancelCommand_WhenNotRunning_IsSafe()
    {
        var (vm, _, _) = Create();
        // Should not throw
        await CancelCommandAsync(vm);
        Assert.False(vm.IsRunning);
    }
}
