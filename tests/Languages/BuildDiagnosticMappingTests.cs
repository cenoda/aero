using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Services.Build;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.Languages;

/// <summary>
/// Tests for the build → Diagnostic mapping contract (R2.11).
/// Verifies that:
///   1. ParsedError (1-based) maps to Diagnostic.TextRange (0-based) correctly.
///   2. Build and LSP diagnostics coexist in DiagnosticStore for the same file.
///   3. ClearSource("build") removes only build diagnostics, not LSP ones.
///   4. ClearSource runs before each build so stale errors don't accumulate.
/// </summary>
public class BuildDiagnosticMappingTests
{
    /// <summary>
    /// Simulates the ShellViewModel mapping from ParsedError to Diagnostic,
    /// verifying the 0-based TextRange contract (R2.1 fix).
    /// </summary>
    private static Diagnostic MapParsedErrorToDiagnostic(ParsedError e)
    {
        return new Diagnostic(
            e.Severity == BuildSeverity.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
            e.FilePath,
            // Convert MSBuild's 1-based line/column to 0-based (R2.1)
            new TextRange(e.Line - 1, e.Column - 1, e.Line - 1, e.Column - 1),
            e.Message,
            "build",
            e.Code);
    }

    [Fact]
    public void ParsedError_Line5_Column17_MapsTo_Range_4_16()
    {
        var parsed = new ParsedError("/abs/Program.cs", 5, 17, "CS0029", "Cannot implicitly convert", BuildSeverity.Error);
        var diagnostic = MapParsedErrorToDiagnostic(parsed);

        Assert.Equal(4, diagnostic.Range.StartLine);
        Assert.Equal(16, diagnostic.Range.StartCharacter);
        Assert.Equal(4, diagnostic.Range.EndLine);
        Assert.Equal(16, diagnostic.Range.EndCharacter);
        Assert.Equal("build", diagnostic.Source);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void ParsedError_Line1_Column1_MapsTo_Range_0_0()
    {
        var parsed = new ParsedError("/abs/File.cs", 1, 1, "CS0001", "First error", BuildSeverity.Warning);
        var diagnostic = MapParsedErrorToDiagnostic(parsed);

        Assert.Equal(0, diagnostic.Range.StartLine);
        Assert.Equal(0, diagnostic.Range.StartCharacter);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("build", diagnostic.Source);
    }

    [Fact]
    public void ParsedError_WarningSeverity_MapsTo_Warning()
    {
        var parsed = new ParsedError("/abs/File.cs", 10, 5, "CS0168", "Variable unused", BuildSeverity.Warning);
        var diagnostic = MapParsedErrorToDiagnostic(parsed);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(9, diagnostic.Range.StartLine);
        Assert.Equal(4, diagnostic.Range.StartCharacter);
    }

    [Fact]
    public void LocationText_Display_AddsOne_ToZeroBasedRange()
    {
        // Diagnostic.LocationText adds +1 to display 1-based to the user
        var diagnostic = MapParsedErrorToDiagnostic(
            new ParsedError("/abs/File.cs", 5, 17, "CS0029", "msg", BuildSeverity.Error));

        Assert.Equal("Ln 5, Col 17", diagnostic.LocationText);
    }

    // -------------------------------------------------------------------
    // DiagnosticStore coexistence tests (R1.1)
    // -------------------------------------------------------------------

    [Fact]
    public void BuildAndLspDiagnostics_Coexist_ForSameFile()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);
        var uri = "file:///test.cs";

        var lspDiags = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(2, 0, 2, 10), "LSP error", "lsp", "CS0001")
        };
        var buildDiags = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(4, 16, 4, 16), "Build error", "build", "CS0029")
        };

        store.SetDiagnostics("lsp", uri, lspDiags);
        store.SetDiagnostics("build", uri, buildDiags);

        // Both should be present when querying by file URI
        var all = store.GetDiagnostics(uri);
        Assert.Equal(2, all.Count);

        // GetAllDiagnostics should merge them
        var workspace = store.GetAllDiagnostics();
        Assert.Equal(2, workspace.Count);
    }

    [Fact]
    public void ClearSource_BuildOnly_RemovesBuildDiagnostics()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);
        var uri = "file:///test.cs";

        store.SetDiagnostics("lsp", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "LSP error", "lsp")
        });
        store.SetDiagnostics("build", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(4, 16, 4, 16), "Build error", "build")
        });

        // Clear only build diagnostics
        store.ClearSource("build");

        // LSP diagnostics should survive
        var remaining = store.GetDiagnostics(uri);
        Assert.Single(remaining);
        Assert.Equal("LSP error", remaining[0].Message);
        Assert.Equal("lsp", remaining[0].Source);
    }

    [Fact]
    public void ClearSource_LspOnly_RemovesLspDiagnostics()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);
        var uri = "file:///test.cs";

        store.SetDiagnostics("lsp", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "LSP error", "lsp")
        });
        store.SetDiagnostics("build", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Warning, uri, new TextRange(4, 16, 4, 16), "Build warning", "build")
        });

        store.ClearSource("lsp");

        var remaining = store.GetDiagnostics(uri);
        Assert.Single(remaining);
        Assert.Equal("Build warning", remaining[0].Message);
        Assert.Equal("build", remaining[0].Source);
    }

    [Fact]
    public void ClearSource_StaleBuildErrors_DontAccumulate_AcrossBuilds()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);
        var uri = "file:///test.cs";

        // Simulate first build with errors
        store.SetDiagnostics("build", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "Old error", "build", "CS0001")
        });
        Assert.Single(store.GetDiagnostics(uri));

        // Simulate ClearSource("build") before second build (ShellViewModel pattern)
        store.ClearSource("build");
        Assert.Empty(store.GetDiagnostics(uri));

        // Second build with different errors
        store.SetDiagnostics("build", uri, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Warning, uri, new TextRange(9, 4, 9, 4), "New warning", "build", "CS0168")
        });
        var diags = store.GetDiagnostics(uri);
        Assert.Single(diags);
        Assert.Equal("New warning", diags[0].Message);
    }

    [Fact]
    public void ClearSource_DoesNotAffectOtherSources()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);
        var uriA = "file:///a.cs";
        var uriB = "file:///b.cs";

        store.SetDiagnostics("build", uriA, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uriA, new TextRange(0, 0, 0, 5), "A build error", "build")
        });
        store.SetDiagnostics("lsp", uriA, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uriA, new TextRange(0, 0, 0, 5), "A lsp error", "lsp")
        });
        store.SetDiagnostics("build", uriB, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Warning, uriB, new TextRange(0, 0, 0, 5), "B build warning", "build")
        });

        store.ClearSource("build");

        // Only LSP diagnostics for A should remain
        var remainingA = store.GetDiagnostics(uriA);
        Assert.Single(remainingA);
        Assert.Equal("A lsp error", remainingA[0].Message);

        // B should have no diagnostics
        Assert.Empty(store.GetDiagnostics(uriB));
    }

    // -------------------------------------------------------------------
    // DotNetBuildService + DiagnosticStore integration
    // -------------------------------------------------------------------

    [Fact]
    public async Task DotNetBuildService_Build_ParsesErrors_IntoZeroBasedDiagnostics()
    {
        // Simulate the full pipeline: BuildAsync → ParseErrors → map to Diagnostics
        var lines = new List<string>
        {
            "/abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert type 'string' to 'int' [/abs/path/probe.csproj]",
            "/abs/path/File.cs(7,13): warning CS0168: The variable 'y' is declared but never used [project]"
        };

        var runner = new StubBuildProcessRunner(lines, 1);
        var service = new DotNetBuildService(runner);
        var options = new BuildOptions("/abs/path");
        var result = await service.BuildAsync(options, _ => { }, CancellationToken.None);

        Assert.Equal(2, result.Errors.Count);

        // Map errors to diagnostics (same as ShellViewModel)
        var diagnostics = result.Errors.Select(e => MapParsedErrorToDiagnostic(e)).ToList();

        // First error: line 5, col 17 → range(4, 16)
        Assert.Equal(4, diagnostics[0].Range.StartLine);
        Assert.Equal(16, diagnostics[0].Range.StartCharacter);
        Assert.Equal("build", diagnostics[0].Source);
        Assert.Equal("CS0029", diagnostics[0].Code);

        // Warning: line 7, col 13 → range(6, 12)
        Assert.Equal(6, diagnostics[1].Range.StartLine);
        Assert.Equal(12, diagnostics[1].Range.StartCharacter);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[1].Severity);
    }

    [Fact]
    public async Task DotNetBuildService_Build_StreamsOutput_ViaOnLine()
    {
        var capturedLines = new List<string>();
        var outputLines = new List<string>
        {
            "Microsoft (R) Build Engine version 17.0",
            "Build succeeded.",
            "    0 Warning(s)",
            "    0 Error(s)"
        };

        var runner = new StubBuildProcessRunner(outputLines, 0);
        var service = new DotNetBuildService(runner);
        var options = new BuildOptions("/abs/path");
        var result = await service.BuildAsync(options, line => capturedLines.Add(line), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(4, capturedLines.Count);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Stub IProcessRunner for build tests — emits lines and returns configured exit code.
    /// </summary>
    private class StubBuildProcessRunner : Aero.Terminal.IProcessRunner
    {
        private readonly IReadOnlyList<string> _lines;
        private readonly int _exitCode;

        public StubBuildProcessRunner(IReadOnlyList<string> lines, int exitCode)
        {
            _lines = lines;
            _exitCode = exitCode;
        }

        public Task<int> RunAsync(
            string executable,
            string arguments,
            string? workingDirectory,
            Action<string> onLine,
            CancellationToken cancellationToken = default)
        {
            foreach (var line in _lines)
                onLine?.Invoke(line);
            return Task.FromResult(_exitCode);
        }
    }
}
