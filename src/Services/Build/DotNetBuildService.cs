using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aero.Terminal;

namespace Aero.Services.Build;

/// <summary>
/// Build service for .NET projects using dotnet CLI.
/// </summary>
public class DotNetBuildService : IBuildService
{
    private readonly IProcessRunner _processRunner;

    /// <summary>
    /// Locale-agnostic regex for MSBuild diagnostic lines. Matches the format:
    ///   file(line,col): SEVERITY_WORD CODE: message [project]
    /// The SEVERITY_WORD is matched as any non-whitespace token so the regex
    /// works regardless of the user's locale. Severity detection is performed
    /// separately via <see cref="IsErrorSeverity"/>.
    /// </summary>
    private static readonly Regex ErrorLineRegex = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>\S+)\s+(?<code>[A-Za-z]+\d+):\s+(?<msg>.+?)(\s+\[[^\]]+\])?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Common translations of the MSBuild "error" severity keyword.
    /// Used as a best-effort fallback when the locale is not English.
    /// If the word is not recognized, the diagnostic is still captured
    /// with Warning severity rather than being silently dropped.
    /// </summary>
    private static readonly HashSet<string> ErrorWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "error",       // English
        "fehler",      // German
        "erreur",      // French
        "errore",      // Italian
        "error",       // Spanish / Portuguese (same as English)
        "ошибка",      // Russian
        "エラー",       // Japanese
        "错误",        // Chinese (Simplified)
        "오류",        // Korean
    };

    public DotNetBuildService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Name => "dotnet";

    public async Task<BuildResult> BuildAsync(
        BuildOptions options,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        var capturedLines = new List<string>();
        Action<string> captureLine = line =>
        {
            capturedLines.Add(line);
            onLine?.Invoke(line);
        };

        var arguments = BuildArguments(options);
        var stopwatch = Stopwatch.StartNew();

        var exitCode = await _processRunner.RunAsync(
            "dotnet",
            arguments,
            options.WorkingDirectory,
            captureLine,
            cancellationToken);

        stopwatch.Stop();

        var errors = ParseErrors(capturedLines);
        var success = exitCode == 0;

        return new BuildResult(success, exitCode, stopwatch.Elapsed, errors);
    }

    public IReadOnlyList<ParsedError> ParseErrors(IReadOnlyList<string> outputLines)
    {
        var errors = new List<ParsedError>();

        foreach (var line in outputLines)
        {
            var match = ErrorLineRegex.Match(line);
            if (!match.Success)
                continue;

            var filePath = match.Groups["file"].Value;
            var lineNum = int.Parse(match.Groups["line"].Value);
            var column = int.Parse(match.Groups["col"].Value);
            var code = match.Groups["code"].Value;
            var message = match.Groups["msg"].Value;

            var severity = IsErrorSeverity(match.Groups["sev"].Value)
                ? BuildSeverity.Error
                : BuildSeverity.Warning;

            errors.Add(new ParsedError(filePath, lineNum, column, code, message, severity));
        }

        return errors;
    }

    /// <summary>
    /// Determine whether a severity keyword from MSBuild output indicates an error.
    /// Checks a set of known translations; if unknown, assumes warning.
    /// </summary>
    private static bool IsErrorSeverity(string severityWord) =>
        ErrorWords.Contains(severityWord);

    private static string BuildArguments(BuildOptions options)
    {
        var args = "build";

        if (!string.IsNullOrEmpty(options.TargetPath))
        {
            args += $" \"{options.TargetPath}\"";
        }

        if (options.IsCleanBuild)
        {
            args += " --no-incremental";
        }

        // Suppress summary to keep output clean for parsing
        args += " /clp:NoSummary";

        return args;
    }
}
