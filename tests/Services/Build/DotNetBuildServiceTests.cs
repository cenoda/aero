using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aero.Services.Build;
using Aero.Terminal;
using Xunit;

namespace Aero.Tests.Services.Build;

/// <summary>
/// Unit tests for <see cref="DotNetBuildService"/>.
/// </summary>
public class DotNetBuildServiceTests
{
    /// <summary>
    /// Stub IProcessRunner that returns a configurable exit code and output lines.
    /// </summary>
    private class StubProcessRunner : IProcessRunner
    {
        private readonly int _exitCode;
        private readonly IReadOnlyList<string> _outputLines;

        public StubProcessRunner(int exitCode, IReadOnlyList<string> outputLines)
        {
            _exitCode = exitCode;
            _outputLines = outputLines;
        }

        public Task<int> RunAsync(
            string executable,
            string arguments,
            string? workingDirectory,
            Action<string> onLine,
            CancellationToken cancellationToken = default)
        {
            foreach (var line in _outputLines)
            {
                onLine?.Invoke(line);
            }
            return Task.FromResult(_exitCode);
        }
    }

    // -------------------------------------------------------------------
    // ParseErrors Tests
    // -------------------------------------------------------------------

    [Fact]
    public void ParseErrors_ParsesErrorLine()
    {
        var runner = new StubProcessRunner(1, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var lines = new List<string>
        {
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert type 'string' to 'int' [/abs/path/probe.csproj]"
        };

        var errors = service.ParseErrors(lines);

        Assert.Single(errors);
        Assert.Equal("/abs/path/Program.cs", errors[0].FilePath);
        Assert.Equal(5, errors[0].Line);
        Assert.Equal(17, errors[0].Column);
        Assert.Equal("CS0029", errors[0].Code);
        Assert.Equal(BuildSeverity.Error, errors[0].Severity);
    }

    [Fact]
    public void ParseErrors_ParsesWarningLine()
    {
        var runner = new StubProcessRunner(0, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var lines = new List<string>
        {
            "/abs/path/File.cs(7,13): warning CS0168: The variable 'y' is declared but never used [project]"
        };

        var errors = service.ParseErrors(lines);

        Assert.Single(errors);
        Assert.Equal("/abs/path/File.cs", errors[0].FilePath);
        Assert.Equal(7, errors[0].Line);
        Assert.Equal(13, errors[0].Column);
        Assert.Equal("CS0168", errors[0].Code);
        Assert.Equal(BuildSeverity.Warning, errors[0].Severity);
    }

    [Fact]
    public void ParseErrors_IgnoresNonDiagnosticLines()
    {
        var runner = new StubProcessRunner(0, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var lines = new List<string>
        {
            "Build started...",
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert [/abs/path/probe.csproj]",
            "Some other output",
            "    at Some.Method()",
            ""
        };

        var errors = service.ParseErrors(lines);

        Assert.Single(errors);
        Assert.Equal("CS0029", errors[0].Code);
    }

    [Fact]
    public void ParseErrors_StripsTrailingProjectBracket()
    {
        var runner = new StubProcessRunner(1, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var lines = new List<string>
        {
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert type 'string' to 'int' [/abs/path/probe.csproj]"
        };

        var errors = service.ParseErrors(lines);

        Assert.Single(errors);
        // Message should NOT include the trailing " [project]" part
        Assert.DoesNotContain("[", errors[0].Message);
        Assert.DoesNotContain("]", errors[0].Message);
    }

    [Fact]
    public void ParseErrors_MultipleErrorsAndWarnings()
    {
        var runner = new StubProcessRunner(1, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var lines = new List<string>
        {
            "/abs/path/A.cs(1,1): error CS0001: First error [proj.csproj]",
            "/abs/path/B.cs(2,2): warning CS0002: First warning [proj.csproj]",
            "/abs/path/A.cs(3,3): error CS0003: Second error [proj.csproj]",
        };

        var errors = service.ParseErrors(lines);

        Assert.Equal(3, errors.Count);
        Assert.Equal(BuildSeverity.Error, errors[0].Severity);
        Assert.Equal(BuildSeverity.Warning, errors[1].Severity);
        Assert.Equal(BuildSeverity.Error, errors[2].Severity);
    }

    // -------------------------------------------------------------------
    // BuildAsync Tests
    // -------------------------------------------------------------------

    [Fact]
    public async Task BuildAsync_ReturnsSuccess_WhenExitCodeZero()
    {
        var runner = new StubProcessRunner(0, new List<string> { "Build succeeded." });
        var service = new DotNetBuildService(runner);

        var options = new BuildOptions("/abs/path/proj.csproj");
        var result = await service.BuildAsync(options, _ => { }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task BuildAsync_ReturnsFailure_WhenExitCodeNonZero()
    {
        var runner = new StubProcessRunner(1, new List<string>
        {
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert type 'string' to 'int' [/abs/path/probe.csproj]"
        });
        var service = new DotNetBuildService(runner);

        var options = new BuildOptions("/abs/path/proj.csproj");
        var result = await service.BuildAsync(options, _ => { }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task BuildAsync_ParsesErrorsFromCapturedOutput()
    {
        var runner = new StubProcessRunner(1, new List<string>
        {
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert type 'string' to 'int' [/abs/path/probe.csproj]",
            "/abs/path/Other.cs(10,5): warning CS0168: Variable declared but never used [proj.csproj]"
        });
        var service = new DotNetBuildService(runner);

        var options = new BuildOptions("/abs/path/proj.csproj");
        var result = await service.BuildAsync(options, _ => { }, CancellationToken.None);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("CS0029", result.Errors[0].Code);
        Assert.Equal("CS0168", result.Errors[1].Code);
    }

    [Fact]
    public async Task BuildAsync_StreamsOutputViaOnLine()
    {
        var capturedLines = new List<string>();
        var runner = new StubProcessRunner(0, new List<string> { "Line 1", "Line 2", "Line 3" });
        var service = new DotNetBuildService(runner);

        var options = new BuildOptions("/abs/path/proj.csproj");
        await service.BuildAsync(options, line => capturedLines.Add(line), CancellationToken.None);

        Assert.Equal(3, capturedLines.Count);
        Assert.Equal("Line 1", capturedLines[0]);
        Assert.Equal("Line 2", capturedLines[1]);
        Assert.Equal("Line 3", capturedLines[2]);
    }

    [Fact]
    public async Task BuildAsync_MeasuresElapsedTime()
    {
        var runner = new StubProcessRunner(0, Array.Empty<string>());
        var service = new DotNetBuildService(runner);

        var options = new BuildOptions("/abs/path/proj.csproj");
        var result = await service.BuildAsync(options, _ => { }, CancellationToken.None);

        Assert.True(result.Duration >= TimeSpan.Zero);
    }
}