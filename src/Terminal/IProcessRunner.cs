using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Terminal;

public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="executable"/> with <paramref name="arguments"/>,
    /// streaming each stdout and stderr line to <paramref name="onLine"/>.
    /// </summary>
    /// <param name="workingDirectory">
    /// Working directory for the process. Pass <c>null</c> to use
    /// <see cref="System.IO.Directory.GetCurrentDirectory"/>.
    /// </param>
    /// <returns>
    /// The process exit code, or <c>-1</c> if the process could not be
    /// started (binary not found, access denied, etc.).
    /// </returns>
    Task<int> RunAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken = default);
}
