# Phase 7: Git Integration

> Know what changed. Commit with confidence.

## Goal

Add Git panel, diff viewer, and commit UI using abstraction-first design.

## Entry Condition

- Phase 6 complete (Build & Output)

## Exit Condition

- Git panel shows staged/unstaged changes
- Diff viewer shows inline +/- gutters
- Can stage, unstage, and commit from UI
- Status bar shows current branch
- Modified files show indicators in tabs and file tree

## Architecture (Abstraction-First)

### Interface

```csharp
public interface IGitService
{
    /// <summary>Human-readable name, e.g. "libgit2sharp".</summary>
    string Name { get; }

    // Repository
    Task<GitRepositoryInfo> GetRepositoryInfoAsync(CancellationToken ct);

    // Status
    Task<IReadOnlyList<GitFileStatus>> GetStatusAsync(CancellationToken ct);

    // Stage / Unstage
    Task StageAsync(string filePath, CancellationToken ct);
    Task UnstageAsync(string filePath, CancellationToken ct);

    // Commit
    Task<GitCommitResult> CommitAsync(string message, string authorName, string authorEmail, CancellationToken ct);

    // Branch
    Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(CancellationToken ct);
    Task CheckoutAsync(string branchName, CancellationToken ct);

    // Diff
    Task<GitDiff> GetFileDiffAsync(string filePath, CancellationToken ct);

    // Log
    Task<IReadOnlyList<GitCommitInfo>> GetLogAsync(int count, CancellationToken ct);
}

public record GitRepositoryInfo(
    string RootPath,
    string CurrentBranch,
    bool IsDirty);

public enum GitFileStatusKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Ignored,
    Staged,
    Conflicted
}

public record GitFileStatus(
    string FilePath,
    string? OldFilePath,     // for renames
    GitFileStatusKind StagingStatus,   // WorkDir vs Index
    GitFileStatusKind Status);

public record GitBranchInfo(
    string Name,
    string CanonicalName,
    bool IsCurrent,
    bool IsRemote,
    string? UpstreamName);

public record GitCommitInfo(
    string Sha,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    DateTimeOffset CommitDate);

public record GitCommitResult(
    string Sha,
    bool Success,
    string? Error);

public record GitDiff(
    string FilePath,
    string OldFilePath,
    IReadOnlyList<GitDiffHunk> Hunks);

public record GitDiffHunk(
    int OldStart, int OldCount,
    int NewStart, int NewCount,
    IReadOnlyList<GitDiffLine> Lines);

public enum GitDiffLineKind { Context, Addition, Deletion, Header }

public record GitDiffLine(
    GitDiffLineKind Kind,
    string Content,
    int OldLineNumber,   // -1 if addition
    int NewLineNumber);  // -1 if deletion
```

### Implementations

```
IGitService (interface)
    │
    ├── LibGit2SharpService  ← Phase 7: typed objects
    └── GitCliService        ← Future: git CLI wrapper
```

### Factory

```csharp
public class GitServiceFactory
{
    public IGitService? Detect(string workspacePath)
    {
        // Check for .git directory
        // Return appropriate service
    }
}
```

## Checklist

- [ ] **IGitService interface** — abstraction with repository, file, branch operations
- [ ] **LibGit2SharpService** — implements IGitService
- [ ] **GitServiceFactory** — auto-detect .git directory
- [ ] **Git panel** — staged/unstaged changes list
- [ ] **Diff viewer** — inline diff with +/- gutter
- [ ] Commit UI (message, stage/unstage, commit button)
- [ ] Branch indicator in status bar
- [ ] File modified indicator in editor tab and file tree

## Related Documents

- `docs/LIBRARIES.md` — LibGit2Sharp, DiffPlex
- `docs/architecture/IDE_CORE.md` — Git Integration subsystem
- `docs/design/PANELS_AND_DOCKING.md` — Sidebar panel layout

## Notes

- LibGit2Sharp is preferred over git CLI parsing (typed objects vs string parsing).
- Diff viewer can reuse editor components with custom gutter rendering.
- File modified indicator requires coordination between GitRepository and DocumentManager.
