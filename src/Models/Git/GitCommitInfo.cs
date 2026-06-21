using System;

namespace Aero.Models.Git;

/// <summary>
/// Information about a Git commit.
/// </summary>
public record GitCommitInfo(
    /// <summary>The full SHA-1 hash of the commit.</summary>
    string Sha,

    /// <summary>The commit message (first line or full message).</summary>
    string Message,

    /// <summary>The author's display name.</summary>
    string AuthorName,

    /// <summary>The author's email address.</summary>
    string AuthorEmail,

    /// <summary>The author's timestamp (author date).</summary>
    DateTimeOffset AuthorDate,

    /// <summary>The committer's timestamp (commit date).</summary>
    DateTimeOffset CommitDate);
