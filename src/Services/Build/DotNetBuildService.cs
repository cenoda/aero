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
        var regex = new Regex(
            @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+):\s+(?<msg>.+?)(\s+\[[^\]]+\])?$");

        foreach (var line in outputLines)
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var filePath = match.Groups["file"].Value;
            var lineNum = int.Parse(match.Groups["line"].Value);
            var column = int.Parse(match.Groups["col"].Value);
            var severityStr = match.Groups["sev"].Value;
            var code = match.Groups["code"].Value;
            var message = match.Groups["msg"].Value;

            var severity = severityStr == "error" ? BuildSeverity.Error : BuildSeverity.Warning;

            errors.Add(new ParsedError(filePath, lineNum, column, code, message, severity));
        }

        return errors;
    }

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