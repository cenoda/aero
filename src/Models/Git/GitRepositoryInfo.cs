namespace Aero.Models.Git;

/// <summary>
/// Information about a Git repository.
/// </summary>
public record GitRepositoryInfo(
    /// <summary>The root path of the repository (the directory containing .git).</summary>
    string RootPath,

    /// <summary>The name of the currently checked-out branch.</summary>
    string CurrentBranch,

    /// <summary>True if the working directory has uncommitted changes.</summary>
    bool IsDirty);
