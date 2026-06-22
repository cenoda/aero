using System;
using System.Collections.Generic;

namespace Aero.Services.Git;

/// <summary>
/// A commit in the git graph, containing metadata needed to render a visual
/// branch graph (DAG). Parent SHAs are strings to avoid recursive graph
/// traversal (see R3.1).
/// </summary>
public record GitGraphCommit(
    /// <summary>The full SHA of the commit.</summary>
    string Sha,

    /// <summary>The commit message (short form, first line).</summary>
    string Message,

    /// <summary>The name of the author.</summary>
    string Author,

    /// <summary>The date when the commit was authored.</summary>
    DateTimeOffset AuthorDate,

    /// <summary>
    /// The SHAs of parent commits. For a merge commit this will contain
    /// two entries. For the initial commit it will be empty.
    /// Retrieved as strings only — no recursive traversal (R3.1).
    /// </summary>
    IReadOnlyList<string> ParentShas,

    /// <summary>
    /// Branch names that point to this commit (e.g., "main", "feature/x").
    /// Populated by checking which refs/heads/* reference this commit's SHA.
    /// </summary>
    IReadOnlyList<string> BranchLabels);
