using System.Collections.Generic;

namespace Aero.Models.Git;

/// <summary>
/// The diff for a single file between two versions (e.g., working directory vs index).
/// </summary>
public record GitDiff(
    /// <summary>The path of the file in the new version.</summary>
    string FilePath,

    /// <summary>The original path (for renamed files).</summary>
    string OldFilePath,

    /// <summary>The hunks in this diff.</summary>
    IReadOnlyList<GitDiffHunk> Hunks);
