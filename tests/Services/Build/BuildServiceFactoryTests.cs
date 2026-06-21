using System;
using System.Collections.Generic;
using System.IO;
using Aero.Models.Project;
using Aero.Services.Build;
using Aero.Terminal;
using Xunit;

namespace Aero.Tests.Services.Build;

/// <summary>
/// Unit tests for <see cref="BuildServiceFactory"/>.
/// </summary>
public class BuildServiceFactoryTests
{
    /// <summary>
    /// Stub IProjectLoader that returns configurable projects.
    /// </summary>
    private class StubProjectLoader : Aero.Services.IProjectLoader
    {
        private readonly IReadOnlyList<ProjectInfo> _projects;

        public StubProjectLoader(IReadOnlyList<ProjectInfo> projects)
        {
            _projects = projects;
        }

        public ProjectKind DetectProjectKind(string path)
        {
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                return ProjectKind.Solution;
            if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return ProjectKind.CSharpProject;
            if (path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
                return ProjectKind.NodeProject;
            return ProjectKind.None;
        }

        public IReadOnlyList<ProjectInfo> DetectProjects(string rootPath, System.Threading.CancellationToken ct = default)
        {
            return _projects;
        }
    }

    [Fact]
    public void Detect_ReturnsNull_ForEmptyDirectory()
    {
        var loader = new StubProjectLoader(Array.Empty<ProjectInfo>());
        var dotNet = new DotNetBuildService(new StubProcessRunner());
        var factory = new BuildServiceFactory(loader, dotNet);

        var result = factory.Detect("/empty/workspace");

        Assert.Null(result);
    }

    [Fact]
    public void Detect_ReturnsDotNetBuildService_ForCsproj()
    {
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("/path/proj.csproj", "proj", ProjectKind.CSharpProject)
        };
        var loader = new StubProjectLoader(projects);
        var dotNet = new DotNetBuildService(new StubProcessRunner());
        var factory = new BuildServiceFactory(loader, dotNet);

        var result = factory.Detect("/path");

        Assert.NotNull(result);
        Assert.Equal("dotnet", result.Name);
    }

    [Fact]
    public void Detect_ReturnsDotNetBuildService_ForSln()
    {
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("/path/solution.sln", "solution", ProjectKind.Solution)
        };
        var loader = new StubProjectLoader(projects);
        var dotNet = new DotNetBuildService(new StubProcessRunner());
        var factory = new BuildServiceFactory(loader, dotNet);

        var result = factory.Detect("/path");

        Assert.NotNull(result);
        Assert.Equal("dotnet", result.Name);
    }

    [Fact]
    public void Detect_PrefersSlnOverCsproj()
    {
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("/path/proj.csproj", "proj", ProjectKind.CSharpProject),
            new ProjectInfo("/path/solution.sln", "solution", ProjectKind.Solution)
        };
        var loader = new StubProjectLoader(projects);
        var dotNet = new DotNetBuildService(new StubProcessRunner());
        var factory = new BuildServiceFactory(loader, dotNet);

        var result = factory.Detect("/path");

        Assert.NotNull(result);
        Assert.Equal("dotnet", result.Name);
    }

    [Fact]
    public void Detect_ReturnsNull_ForNodeProjectOnly()
    {
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("/path/package.json", "package", ProjectKind.NodeProject)
        };
        var loader = new StubProjectLoader(projects);
        var dotNet = new DotNetBuildService(new StubProcessRunner());
        var factory = new BuildServiceFactory(loader, dotNet);

        var result = factory.Detect("/path");

        Assert.Null(result);
    }

    /// <summary>
    /// Stub IProcessRunner for DotNetBuildService construction.
    /// </summary>
    private class StubProcessRunner : IProcessRunner
    {
        public System.Threading.Tasks.Task<int> RunAsync(
            string executable,
            string arguments,
            string? workingDirectory,
            Action<string> onLine,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return System.Threading.Tasks.Task.FromResult(0);
        }
    }
}
