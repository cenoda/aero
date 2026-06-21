using System.Collections.Generic;

namespace Aero.Services.Build;

/// <summary>
/// Configuration for a build operation.
/// </summary>
public record BuildOptions(
    string WorkingDirectory,
    string? TargetPath = null,
    bool IsCleanBuild = false);

/// <summary>
/// Result of a build operation.
/// </summary>
public record BuildResult(
    bool Success,
    int ExitCode,
    System.TimeSpan Duration,
    IReadOnlyList<ParsedError> Errors);

/// <summary>
/// Severity level for build diagnostics.
/// </summary>
public enum BuildSeverity
{
    Error,
    Warning,
}

/// <summary>
/// A parsed error or warning from build output.
/// </summary>
public record ParsedError(
    string FilePath,
    int Line,
    int Column,
    string Code,
    string Message,
    BuildSeverity Severity);
