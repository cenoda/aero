using System;

namespace Aero.Models.Git;

/// <summary>
/// The result of a Git commit operation.
/// </summary>
public record GitCommitResult(
    /// <summary>The SHA-1 hash of the new commit, if successful.</summary>
    string Sha,

    /// <summary>True if the commit succeeded.</summary>
    bool Success,

    /// <summary>Error message if the commit failed.</summary>
    string? Error);
