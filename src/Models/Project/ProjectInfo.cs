namespace Aero.Models.Project;

/// <summary>
/// The kind of project recognized by <c>IProjectLoader</c>. Phase 2 only
/// classifies by file extension — full parsing is deferred to Phase 6.
/// </summary>
public enum ProjectKind
{
    /// <summary>Path did not match any known project file.</summary>
    None,
    /// <summary>A Visual Studio solution file (<c>.sln</c>).</summary>
    Solution,
    /// <summary>A C# project file (<c>.csproj</c>).</summary>
    CSharpProject,
    /// <summary>A Node.js package manifest (<c>package.json</c>).</summary>
    NodeProject,
}

/// <summary>
/// A lightweight description of a recognized project. Returned by
/// <c>IProjectLoader.DetectProjects</c> so the file explorer can highlight
/// project roots with appropriate icons. Carries no children — the tree is
/// still enumerated by the file system service.
/// </summary>
/// <param name="Path">Absolute path to the project file.</param>
/// <param name="Name">Display name (typically the file name without extension).</param>
/// <param name="Kind">Detected project kind.</param>
public record ProjectInfo(
    string Path,
    string Name,
    ProjectKind Kind);
