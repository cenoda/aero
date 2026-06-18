using System;
using System.IO;
using System.Linq;
using System.Threading;
using Aero.Models.Project;
using Aero.Services;
using Xunit;

namespace Aero.Tests.Services;

public class ProjectLoaderTests : System.IDisposable
{
    private readonly string _root;
    private readonly ProjectLoader _loader = new();

    public ProjectLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aero-proj-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // -------------------------------------------------------------------
    // DetectProjectKind — single-file recognition
    // -------------------------------------------------------------------

    [Theory]
    [InlineData("foo.sln", ProjectKind.Solution)]
    [InlineData("foo.SLN", ProjectKind.Solution)] // case-insensitive extension
    [InlineData("foo.csproj", ProjectKind.CSharpProject)]
    [InlineData("foo.CSPROJ", ProjectKind.CSharpProject)]
    [InlineData("package.json", ProjectKind.NodeProject)]
    [InlineData("foo.txt", ProjectKind.None)]
    [InlineData("foo.cs", ProjectKind.None)] // source file is not a project
    [InlineData("Cargo.toml", ProjectKind.None)] // out of scope for Phase 2
    public void DetectProjectKind_RecognizesKnownExtensions(string name, ProjectKind expected)
    {
        var result = _loader.DetectProjectKind(Path.Combine(_root, name));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectProjectKind_EmptyPath_ReturnsNone()
    {
        Assert.Equal(ProjectKind.None, _loader.DetectProjectKind(""));
    }

    // -------------------------------------------------------------------
    // DetectProjects — folder enumeration
    // -------------------------------------------------------------------

    [Fact]
    public void DetectProjects_TopLevel_ReturnsRecognizedOnly()
    {
        File.WriteAllText(Path.Combine(_root, "app.sln"), "");
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "");
        File.WriteAllText(Path.Combine(_root, "package.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "README.md"), "");

        var projects = _loader.DetectProjects(_root);

        Assert.Equal(3, projects.Count);
        Assert.Contains(projects, p => p.Kind == ProjectKind.Solution && p.Name == "app.sln");
        Assert.Contains(projects, p => p.Kind == ProjectKind.CSharpProject && p.Name == "app.csproj");
        Assert.Contains(projects, p => p.Kind == ProjectKind.NodeProject && p.Name == "package.json");
        Assert.DoesNotContain(projects, p => p.Name == "README.md");
    }

    [Fact]
    public void DetectProjects_NoProjectFiles_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "");
        File.WriteAllText(Path.Combine(_root, "b.cs"), "");

        var projects = _loader.DetectProjects(_root);

        Assert.Empty(projects);
    }

    [Fact]
    public void DetectProjects_EmptyDirectory_ReturnsEmpty()
    {
        Assert.Empty(_loader.DetectProjects(_root));
    }

    [Fact]
    public void DetectProjects_NonexistentDirectory_ReturnsEmpty()
    {
        var bogus = Path.Combine(_root, "does-not-exist");
        Assert.Empty(_loader.DetectProjects(bogus));
    }

    [Fact]
    public void DetectProjects_EmptyPath_ReturnsEmpty()
    {
        Assert.Empty(_loader.DetectProjects(""));
    }

    [Fact]
    public void DetectProjects_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _loader.DetectProjects(_root, cts.Token));
    }
}
