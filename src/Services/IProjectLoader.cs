using System.Collections.Generic;
using System.Threading;
using Aero.Models.Project;

namespace Aero.Services;

/// <summary>
/// Lightweight project recognition. Phase 2 only classifies by extension —
/// full MSBuild / Node parsing is deferred to Phase 6.
/// </summary>
public interface IProjectLoader
{
    /// <summary>Classify a single path by its extension.</summary>
    ProjectKind DetectProjectKind(string path);

    /// <summary>
    /// Enumerate <paramref name="rootPath"/> (one level deep) and return any
    /// recognized project files. Used by the file explorer to highlight roots
    /// with project-specific icons.
    /// </summary>
    IReadOnlyList<ProjectInfo> DetectProjects(string rootPath, CancellationToken ct = default);
}
