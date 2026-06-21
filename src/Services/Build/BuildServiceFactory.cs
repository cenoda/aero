using System;
using System.Linq;
using Aero.Models.Project;

namespace Aero.Services.Build;

/// <summary>
/// Factory that detects the workspace build system and returns the
/// appropriate IBuildService implementation.
/// </summary>
public class BuildServiceFactory
{
    private readonly IProjectLoader _projectLoader;
    private readonly DotNetBuildService _dotNetBuildService;

    public BuildServiceFactory(IProjectLoader projectLoader, DotNetBuildService dotNetBuildService)
    {
        _projectLoader = projectLoader ?? throw new ArgumentNullException(nameof(projectLoader));
        _dotNetBuildService = dotNetBuildService ?? throw new ArgumentNullException(nameof(dotNetBuildService));
    }

    /// <summary>
    /// Detect the build system for the given workspace path.
    /// Returns null if no recognized build system is found.
    /// </summary>
    public IBuildService? Detect(string workspacePath)
    {
        var projects = _projectLoader.DetectProjects(workspacePath);

        // Prefer .sln over .csproj
        if (projects.Any(p => p.Kind == ProjectKind.Solution))
            return _dotNetBuildService;

        if (projects.Any(p => p.Kind == ProjectKind.CSharpProject))
            return _dotNetBuildService;

        return null;
    }
}