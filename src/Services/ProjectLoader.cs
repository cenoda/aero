using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Aero.Models.Project;

namespace Aero.Services;

/// <summary>
/// Extension-based project recognition. Recognized types:
/// <list type="bullet">
///   <item><c>.sln</c> → <see cref="ProjectKind.Solution"/></item>
///   <item><c>.csproj</c> → <see cref="ProjectKind.CSharpProject"/></item>
///   <item><c>package.json</c> → <see cref="ProjectKind.NodeProject"/></item>
/// </list>
/// Anything else → <see cref="ProjectKind.None"/>. Full parsing is Phase 6.
/// </summary>
public sealed class ProjectLoader : IProjectLoader
{
    /// <inheritdoc />
    public ProjectKind DetectProjectKind(string path)
    {
        if (string.IsNullOrEmpty(path))
            return ProjectKind.None;

        // Exact-name match for files without an extension but with a known
        // basename (e.g. "package.json"). Comparison is ordinal — package.json
        // is universally lowercase by convention.
        var name = Path.GetFileName(path);
        if (string.Equals(name, "package.json", StringComparison.Ordinal))
            return ProjectKind.NodeProject;

        var ext = Path.GetExtension(path);
        return ext.ToLowerInvariant() switch
        {
            ".sln" => ProjectKind.Solution,
            ".csproj" => ProjectKind.CSharpProject,
            _ => ProjectKind.None,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ProjectInfo> DetectProjects(string rootPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return Array.Empty<ProjectInfo>();

        // Check cancellation up front — same reason as FileSystemService.
        // An empty directory's enumerator body never runs, so the per-iteration
        // check would not see a pre-cancelled token.
        ct.ThrowIfCancellationRequested();

        var results = new List<ProjectInfo>();

        // One level deep — that's enough to surface top-level solutions and
        // package manifests without re-enumerating every node_modules.
        foreach (var file in Directory.EnumerateFiles(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            var kind = DetectProjectKind(file);
            if (kind == ProjectKind.None)
                continue;

            results.Add(new ProjectInfo(
                Path: file,
                Name: DisplayNameFor(file, kind),
                Kind: kind));
        }

        return results;
    }

    private static string DisplayNameFor(string path, ProjectKind kind)
    {
        var name = Path.GetFileName(path);
        // ".sln" and ".csproj" keep their extension — that's what users see in
        // Solution Explorer. "package.json" is already self-describing.
        return kind switch
        {
            ProjectKind.NodeProject => "package.json",
            _ => name,
        };
    }
}
