namespace Aero.Models.Git;

/// <summary>
/// A single line in a git diff hunk.
/// </summary>
public record GitDiffLine(
    /// <summary>The kind of line (context, addition, deletion, header).</summary>
    GitDiffLineKind Kind,

    /// <summary>The content of the line (including +/- prefix if applicable).</summary>
    string Content,

    /// <summary>The line number in the old file (-1 for additions).</summary>
    int OldLineNumber,

    /// <summary>The line number in the new file (-1 for deletions).</summary>
    int NewLineNumber);
