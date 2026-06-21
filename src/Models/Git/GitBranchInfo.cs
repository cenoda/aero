namespace Aero.Models.Git;

/// <summary>
/// Information about a Git branch.
/// </summary>
public record GitBranchInfo(
    /// <summary>The display name of the branch (e.g., "main").</summary>
    string Name,

    /// <summary>The canonical full name (e.g., "refs/heads/main").</summary>
    string CanonicalName,

    /// <summary>True if this is the currently checked-out branch.</summary>
    bool IsCurrent,

    /// <summary>True if this is a remote-tracking branch (e.g., "origin/main").</summary>
    bool IsRemote,

    /// <summary>The upstream branch name (e.g., "main"), if set.</summary>
    string? UpstreamName);
