namespace Aero.Models.Git;

/// <summary>
/// The status of a file in a Git repository, tracking both its state in the
/// working directory and in the staging area (index).
/// </summary>
public record GitFileStatus(
    /// <summary>The path of the file relative to the repository root.</summary>
    string FilePath,

    /// <summary>The original path (for renamed or copied files).</summary>
    string? OldFilePath,

    /// <summary>The status of the file in the staging area (index) — WorkDir vs Index.</summary>
    GitFileStatusKind StagingStatus,

    /// <summary>The status of the file in the working directory.</summary>
    GitFileStatusKind Status);
