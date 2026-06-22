using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Git;
using LibGit2Sharp;

namespace Aero.Services.Git;

/// <summary>
/// LibGit2Sharp implementation of IGitService.
/// Thread-safe: all repository access is serialized via SemaphoreSlim.
/// </summary>
public sealed class LibGit2SharpService : IGitService
{
    private readonly string _gitDir;
    private readonly string _repositoryRoot;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Repository? _repository;
    private bool _disposed;

    public string Name => "LibGit2Sharp";

    public LibGit2SharpService(string gitDir, string repositoryRoot)
    {
        _gitDir = gitDir ?? throw new ArgumentNullException(nameof(gitDir));
        _repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));

        // Try to open repository, but handle missing/invalid repo gracefully (R1.2, R1.4 fix)
        try
        {
            // Use repositoryRoot instead of gitDir - LibGit2Sharp finds .git automatically
            // Opening via .git dir directly can cause empty branch enumeration on some versions
            _repository = new Repository(repositoryRoot);
        }
        catch (InvalidOperationException)
        {
            // Path exists but isn't a valid git repository
            throw new GitServiceUnavailableException(
                "The path exists but is not a valid Git repository.",
                new RepositoryNotFoundException(gitDir));
        }
        catch (RepositoryNotFoundException)
        {
            // Re-throw as our custom exception for consistent handling
            throw new GitServiceUnavailableException(
                "The path does not point at a valid Git repository.",
                new RepositoryNotFoundException(gitDir));
        }
        catch (Exception ex) when (
            ex is NotSupportedException or
            DllNotFoundException or
            BadImageFormatException or
            TypeInitializationException)
        {
            // Native library issues (the original R1.2 case)
            throw new GitServiceUnavailableException(
                "Git native library (libgit2) could not be loaded. Ensure libgit2 dependencies are installed.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<GitRepositoryInfo> GetRepositoryInfoAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var head = repo.Head;
            var branchName = head?.FriendlyName ?? "(detached)";
            var isDirty = repo.RetrieveStatus().IsDirty;

            return new GitRepositoryInfo(_repositoryRoot, branchName, isDirty);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitFileStatus>> GetStatusAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var statuses = repo.RetrieveStatus();
            var result = new List<GitFileStatus>();

            foreach (var entry in statuses)
            {
                // Map index-side and workdir-side separately (Issue #2 fix)
                var indexStatus = MapFileStatusToIndex(entry.State);
                var workdirStatus = MapFileStatusToWorkdir(entry.State);

                result.Add(new GitFileStatus(
                    entry.FilePath,
                    null, // OldFilePath not available in 0.30
                    indexStatus,
                    workdirStatus));
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task StageAsync(string filePath, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_repositoryRoot, filePath);
            Commands.Stage(repo, absolutePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task UnstageAsync(string filePath, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_repositoryRoot, filePath);
            Commands.Unstage(repo, absolutePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<GitCommitResult> CommitAsync(string message, string authorName, string authorEmail, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);

            try
            {
                var commit = repo.Commit(message, author, author);
                return new GitCommitResult(commit.Sha, true, null);
            }
            catch (Exception ex)
            {
                return new GitCommitResult(string.Empty, false, ex.Message);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var result = new List<GitBranchInfo>();

            // Read branch refs directly from the filesystem (.git/refs/heads/ and packed-refs).
            // This avoids LibGit2Sharp 0.30 bugs where repo.Branches hangs and
            // repo.Refs.FromGlob returns empty on some Linux environments.
            var headSha = repo.Head?.Tip?.Sha;
            var branchMap = BuildBranchRefMap();

            foreach (var kvp in branchMap)
            {
                var isCurrent = string.Equals(kvp.Key, headSha, StringComparison.Ordinal);
                result.Add(new GitBranchInfo(
                    kvp.Value,
                    $"refs/heads/{kvp.Value}",
                    isCurrent,
                    IsRemote: false,
                    UpstreamName: null));
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task CheckoutAsync(string branchName, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var branch = repo.Branches[branchName];
            if (branch == null)
                throw new InvalidOperationException($"Branch '{branchName}' not found.");

            try
            {
                Commands.Checkout(repo, branch);
            }
            // Issue #7 fix: Check exception type instead of fragile string matching
            catch (LibGit2SharpException ex) when (
                ex.Message.Contains("conflict") || 
                ex.Message.Contains("Your local changes") ||
                ex.Message.Contains("cannot checkout"))
            {
                // R1.7: Handle checkout conflicts gracefully
                throw new InvalidOperationException(
                    "Cannot switch branch: you have uncommitted changes. Commit or stash first.", ex);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<GitDiff> GetFileDiffAsync(string filePath, CancellationToken ct)
    {
        // Issue #1 fix: take semaphore inside Task.Run
        return await Task.Run(async () =>
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

                var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_repositoryRoot, filePath);
                var relativePath = Path.GetRelativePath(_repositoryRoot, absolutePath);

                // Issue #1 fix: Check if file is staged by looking at status
                // Get all statuses and check if the file has staged changes
                var allStatuses = repo.RetrieveStatus();
                FileStatus? fileStatus = null;
                foreach (var s in allStatuses)
                {
                    if (s.FilePath.Equals(relativePath, StringComparison.Ordinal))
                    {
                        fileStatus = s.State;
                        break;
                    }
                }

                // Determine diff targets based on whether file is staged
                // If file is in index (staged), compare Index vs HEAD
                // Otherwise, compare WorkingDirectory vs HEAD
                var headTip = repo.Head.Tip;
                var isStaged = fileStatus.HasValue && 
                    (fileStatus.Value.HasFlag(FileStatus.ModifiedInIndex) || 
                     fileStatus.Value.HasFlag(FileStatus.NewInIndex));
                var targets = isStaged ? DiffTargets.Index : DiffTargets.WorkingDirectory;
                var patch = repo.Diff.Compare<Patch>(headTip?.Tree, targets);
                var hunks = new List<GitDiffHunk>();

                // R1.6: Cap output at 10,000 lines
                const int MaxLines = 10000;
                int totalLines = 0;

                // Track hunk metadata for Issue #3 fix
                int hunkOldStart = 0, hunkOldCount = 0, hunkNewStart = 0, hunkNewCount = 0;

                foreach (var entry in patch)
                {
                    if (totalLines >= MaxLines)
                        break;

                    // Only include the requested file
                    if (!entry.Path.Equals(relativePath, StringComparison.Ordinal))
                        continue;

                    var hunkLines = new List<GitDiffLine>();
                    var patchContent = entry.Patch;

                    if (!string.IsNullOrEmpty(patchContent))
                    {
                        var lines = patchContent.Split('\n');
                        foreach (var line in lines)
                        {
                            if (totalLines >= MaxLines)
                                break;

                            if (string.IsNullOrEmpty(line))
                                continue;

                            var kind = GitDiffLineKind.Context;
                            if (line.StartsWith("+"))
                                kind = GitDiffLineKind.Addition;
                            else if (line.StartsWith("-"))
                                kind = GitDiffLineKind.Deletion;
                            else if (line.StartsWith("@@"))
                                kind = GitDiffLineKind.Header;

                            // Issue #3 fix: Parse line numbers from hunk header
                            int oldLineNum = -1;
                            int newLineNum = -1;
                            if (kind == GitDiffLineKind.Header)
                            {
                                // Parse @@ -1,3 +1,4 @@ format
                                var parts = line.Split(' ');
                                if (parts.Length >= 2)
                                {
                                    var oldPart = parts[1].TrimStart('-');
                                    var newPart = parts[2].TrimStart('+');
                                    var oldComma = oldPart.IndexOf(',');
                                    var newComma = newPart.IndexOf(',');
                                    if (oldComma > 0)
                                        int.TryParse(oldPart[..oldComma], out oldLineNum);
                                    else if (oldPart.Length > 0)
                                        int.TryParse(oldPart, out oldLineNum);
                                    if (newComma > 0)
                                        int.TryParse(newPart[..newComma], out newLineNum);
                                    else if (newPart.Length > 0)
                                        int.TryParse(newPart, out newLineNum);
                                    
                                    // Store hunk metadata from header
                                    hunkOldStart = oldLineNum;
                                    hunkNewStart = newLineNum;
                                }
                            }

                            hunkLines.Add(new GitDiffLine(kind, line, oldLineNum, newLineNum));
                            totalLines++;
                        }
                    }

                    // Count additions/deletions for hunk metadata
                    hunkOldCount = hunkLines.Count(l => l.Kind == GitDiffLineKind.Deletion || l.Kind == GitDiffLineKind.Context);
                    hunkNewCount = hunkLines.Count(l => l.Kind == GitDiffLineKind.Addition || l.Kind == GitDiffLineKind.Context);

                    // Issue #3 fix: Use actual hunk metadata instead of zeros
                    hunks.Add(new GitDiffHunk(hunkOldStart, hunkOldCount, hunkNewStart, hunkNewCount, hunkLines));
                    break; // Only first matching entry
                }

                return new GitDiff(relativePath, relativePath, hunks);
            }
            finally
            {
                _semaphore.Release();
            }
        }, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitCommitInfo>> GetLogAsync(int count, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var result = new List<GitCommitInfo>();
            var commits = repo.Commits.Take(count);

            foreach (var commit in commits)
            {
                result.Add(new GitCommitInfo(
                    commit.Sha,
                    commit.MessageShort,
                    commit.Author.Name,
                    commit.Author.Email,
                    commit.Author.When,
                    commit.Committer.When));
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string[]> GetConfigAsync(string[] keys, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            var result = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                var configValue = repo.Config.Get<string>(keys[i]);
                result[i] = configValue?.Value ?? string.Empty;
            }
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitGraphCommit>> GetGraphAsync(int count, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            var repo = _repository ?? throw new ObjectDisposedException(nameof(LibGit2SharpService));

            // Build a map of SHA → branch name (reads loose refs + packed-refs — G3 fix)
            var branchRefs = BuildBranchRefMap();

            var result = new List<GitGraphCommit>();
            var commits = repo.Commits.Take(count);

            foreach (var commit in commits)
            {
                // Collect parent SHAs as strings only — no recursive traversal (R3.1)
                var parentShas = commit.Parents
                    .Select(p => p.Sha)
                    .ToList() as IReadOnlyList<string>
                    ?? Array.Empty<string>();

                // Collect branch labels pointing to this commit
                var labels = new List<string>();
                if (branchRefs.TryGetValue(commit.Sha, out var label))
                {
                    labels.Add(label);
                }

                result.Add(new GitGraphCommit(
                    commit.Sha,
                    commit.MessageShort,
                    commit.Author.Name,
                    commit.Author.When,
                    parentShas,
                    labels));
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _semaphore.Wait();
        try
        {
            _repository?.Dispose();
            _repository = null;
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }

    /// <summary>
    /// Builds a SHA → branch name map from loose refs (refs/heads/) and
    /// packed-refs. Used by both GetBranchesAsync and GetGraphAsync.
    /// </summary>
    private Dictionary<string, string> BuildBranchRefMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var localRefsDir = Path.Combine(_gitDir, "refs", "heads");
        var remoteRefsDir = Path.Combine(_gitDir, "refs", "remotes");

        // Loose local refs (refs/heads/*)
        if (Directory.Exists(localRefsDir))
        {
            foreach (var file in Directory.GetFiles(localRefsDir, "*", SearchOption.AllDirectories))
            {
                var sha = File.ReadAllText(file).Trim();
                var relative = Path.GetRelativePath(localRefsDir, file).Replace('\\', '/');
                if (!map.ContainsKey(sha))
                    map[sha] = relative;
            }
        }

        // Loose remote refs (refs/remotes/*)
        if (Directory.Exists(remoteRefsDir))
        {
            foreach (var file in Directory.GetFiles(remoteRefsDir, "*", SearchOption.AllDirectories))
            {
                var sha = File.ReadAllText(file).Trim();
                var relative = Path.GetRelativePath(remoteRefsDir, file).Replace('\\', '/');
                // Prefer remote-tracking label over local to avoid duplicate "master"
                map[sha] = relative;
            }
        }

        // Packed refs (G3 fix)
        var packedRefsPath = Path.Combine(_gitDir, "packed-refs");
        if (File.Exists(packedRefsPath))
        {
            var packedContent = File.ReadAllText(packedRefsPath);
            foreach (var line in packedContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith('#')) continue;
                var parts = line.Split(' ', 2);
                if (parts.Length < 2) continue;
                var sha = parts[0];
                var refName = parts[1].Trim();
                string? friendlyName = null;
                if (refName.StartsWith("refs/remotes/"))
                    friendlyName = refName["refs/remotes/".Length..];
                else if (refName.StartsWith("refs/heads/"))
                    friendlyName = refName["refs/heads/".Length..];

                if (friendlyName == null) continue;
                if (!map.ContainsKey(sha)) // loose refs take precedence
                    map[sha] = friendlyName;
            }
        }

        return map;
    }

    /// <summary>
    /// Map FileStatus to index-side status (staging area).
    /// </summary>
    private static GitFileStatusKind MapFileStatusToIndex(FileStatus status)
    {
        if ((status & FileStatus.NewInIndex) != 0)
            return GitFileStatusKind.Added;
        if ((status & FileStatus.ModifiedInIndex) != 0)
            return GitFileStatusKind.Modified;
        if ((status & FileStatus.DeletedFromIndex) != 0)
            return GitFileStatusKind.Deleted;
        if ((status & FileStatus.RenamedInIndex) != 0)
            return GitFileStatusKind.Renamed;
        return GitFileStatusKind.Unmodified;
    }

    /// <summary>
    /// Map FileStatus to workdir-side status.
    /// </summary>
    private static GitFileStatusKind MapFileStatusToWorkdir(FileStatus status)
    {
        if ((status & FileStatus.NewInWorkdir) != 0)
            return GitFileStatusKind.Untracked;
        if ((status & FileStatus.ModifiedInWorkdir) != 0)
            return GitFileStatusKind.Modified;
        if ((status & FileStatus.DeletedFromWorkdir) != 0)  // Issue #3 fix: added missing case
            return GitFileStatusKind.Deleted;
        if ((status & FileStatus.RenamedInWorkdir) != 0)
            return GitFileStatusKind.Renamed;
        return GitFileStatusKind.Unmodified;
    }
}
