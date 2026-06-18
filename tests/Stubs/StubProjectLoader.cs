using System.Collections.Generic;
using System.IO;
using System.Threading;
using Aero.Models.Project;
using Aero.Services;

namespace Aero.Tests.Stubs;

/// <summary>
/// In-memory <see cref="IProjectLoader"/> for tests. Seeded with the exact
/// (path, kind) pairs the test wants to recognize. Lets FileExplorerViewModel
/// tests verify icon assignment without touching the real disk.
///
/// Why a separate stub: <see cref="ProjectLoader"/> enumerates the real
/// filesystem via <c>Directory.EnumerateFiles</c>, which doesn't see the
/// entries a <see cref="MockFileSystemService"/> has registered in memory.
/// </summary>
public sealed class StubProjectLoader : IProjectLoader
{
    private readonly List<ProjectInfo> _projects = new();

    public void Add(ProjectInfo p) => _projects.Add(p);

    public ProjectKind DetectProjectKind(string path)
    {
        foreach (var p in _projects)
            if (string.Equals(p.Path, path, System.StringComparison.Ordinal))
                return p.Kind;
        return ProjectKind.None;
    }

    public IReadOnlyList<ProjectInfo> DetectProjects(string rootPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Filter by prefix: only return projects that sit under rootPath.
        var prefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
        return _projects.FindAll(p =>
            p.Path.StartsWith(prefix, System.StringComparison.Ordinal) ||
            string.Equals(p.Path, rootPath, System.StringComparison.Ordinal));
    }
}
