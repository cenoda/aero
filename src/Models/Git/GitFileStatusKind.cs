namespace Aero.Models.Git;

/// <summary>
/// Git file status kinds, representing the state of a file in the working directory
/// and/or staging area (index).
/// </summary>
public enum GitFileStatusKind
{
    /// <summary>File has been modified in the working directory.</summary>
    Modified,

    /// <summary>File is new (untracked) in the working directory.</summary>
    Added,

    /// <summary>File has been deleted from the working directory.</summary>
    Deleted,

    /// <summary>File has been renamed (detected by git's rename detection).</summary>
    Renamed,

    /// <summary>File has been copied (detected by git's copy detection).</summary>
    Copied,

    /// <summary>File is untracked (not in the index).</summary>
    Untracked,

    /// <summary>File is ignored by .gitignore.</summary>
    Ignored,

    /// <summary>File is staged (in the index).</summary>
    Staged,

    /// <summary>File has merge conflicts.</summary>
    Conflicted,

    /// <summary>File is unmodified (no changes).</summary>
    Unmodified
}
