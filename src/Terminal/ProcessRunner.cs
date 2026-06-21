using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Aero.Terminal;

public class ProcessRunner : IProcessRunner
{
    public Task<int> RunAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken = default)
    {
        return RunAsyncInternal(
            executable,
            arguments,
            workingDirectory,
            onLine,
            cancellationToken);
    }

    private async Task<int> RunAsyncInternal(
        string executable,
        string arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var workingDir = workingDirectory ?? Directory.GetCurrentDirectory();

            var result = await Cli.Wrap(executable)
                .WithArguments(arguments)
                .WithWorkingDirectory(workingDir)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(onLine))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(onLine))
                .ExecuteAsync(cancellationToken);

            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (Exception ex)
        {
            onLine($"[Error: {ex.Message}]");
            return -1;
        }
    }
}
