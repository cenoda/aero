using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Git;

namespace Aero.Services.Git;

/// <summary>
/// Abstraction for Git operations, enabling different implementations
/// (LibGit2Sharp, git CLI, etc.) via interface-first design.
/// </summary>
public interface IGitService : IDisposable
{
    /// <summary>Human-readable name of the underlying Git implementation.</summary>
    string Name { get; }

    /// <summary>
    /// Gets information about the repository (root path, current branch, dirty state).
    /// </summary>
    Task<GitRepositoryInfo> GetRepositoryInfoAsync(CancellationToken ct);

    /// <summary>
    /// Gets the status of all files in the repository (staged, unstaged, untracked).
    /// </summary>
    Task<IReadOnlyList<GitFileStatus>> GetStatusAsync(CancellationToken ct);

    /// <summary>
    /// Stages a file (adds it to the index).
    /// </summary>
    Task StageAsync(string filePath, CancellationToken ct);

    /// <summary>
    /// Unstages a file (removes it from the index).
    /// </summary>
    Task UnstageAsync(string filePath, CancellationToken ct);

    /// <summary>
    /// Commits the staged changes with the given message and author info.
    /// </summary>
    Task<GitCommitResult> CommitAsync(string message, string authorName, string authorEmail, CancellationToken ct);

    /// <summary>
    /// Gets all branches in the repository.
    /// </summary>
    Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(CancellationToken ct);

    /// <summary>
    /// Checks out the specified branch.
    /// </summary>
    Task CheckoutAsync(string branchName, CancellationToken ct);

    /// <summary>
    /// Gets the diff for a specific file (working directory vs index, or index vs HEAD).
    /// </summary>
    Task<GitDiff> GetFileDiffAsync(string filePath, CancellationToken ct);

    /// <summary>
    /// Gets the commit log (most recent commits).
    /// </summary>
    Task<IReadOnlyList<GitCommitInfo>> GetLogAsync(int count, CancellationToken ct);

    /// <summary>
    /// Gets git config values (e.g., "user.name", "user.email").
    /// Returns null for each key if not found.
    /// </summary>
    Task<string[]> GetConfigAsync(string[] keys, CancellationToken ct);

    /// <summary>
    /// Gets commit graph data (commits with parent SHAs) for rendering a
    /// visual branch graph (DAG). Returns the top <paramref name="count"/>
    /// commits from HEAD. Parent SHAs are strings — no recursive traversal.
    /// </summary>
    Task<IReadOnlyList<GitGraphCommit>> GetGraphAsync(int count, CancellationToken ct);
}
