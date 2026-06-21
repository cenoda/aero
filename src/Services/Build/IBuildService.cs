using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Services.Build;

/// <summary>
/// Abstraction over a build system (e.g. dotnet, npm, cargo).
/// </summary>
public interface IBuildService
{
    /// <summary>Human-readable id, e.g. "dotnet".</summary>
    string Name { get; }

    /// <summary>
    /// Build the target described by <paramref name="options"/>, streaming each
    /// stdout/stderr line to <paramref name="onLine"/>. Never throws for build
    /// failures or a missing toolchain — failures are reported via the result.
    /// </summary>
    Task<BuildResult> BuildAsync(
        BuildOptions options,
        Action<string> onLine,
        CancellationToken cancellationToken);

    /// <summary>Parse captured build output into structured errors/warnings.</summary>
    IReadOnlyList<ParsedError> ParseErrors(IReadOnlyList<string> outputLines);
}