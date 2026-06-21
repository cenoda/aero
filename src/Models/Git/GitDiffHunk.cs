using System.Collections.Generic;

namespace Aero.Models.Git;

/// <summary>
/// A hunk (contiguous block of changes) in a git diff.
/// </summary>
public record GitDiffHunk(
    /// <summary>The starting line number in the old file.</summary>
    int OldStart,

    /// <summary>The number of lines in the old file for this hunk.</summary>
    int OldCount,

    /// <summary>The starting line number in the new file.</summary>
    int NewStart,

    /// <summary>The number of lines in the new file for this hunk.</summary>
    int NewCount,

    /// <summary>The lines in this hunk.</summary>
    IReadOnlyList<GitDiffLine> Lines);
