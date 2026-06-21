namespace Aero.Models.Git;

/// <summary>
/// The kind of line in a git diff (context, addition, deletion, or hunk header).
/// </summary>
public enum GitDiffLineKind
{
    /// <summary>Unchanged line present in both old and new versions.</summary>
    Context,

    /// <summary>Line added in the new version.</summary>
    Addition,

    /// <summary>Line removed in the old version.</summary>
    Deletion,

    /// <summary>Hunk header line (e.g., "@@ -1,3 +1,4 @@").</summary>
    Header
}
