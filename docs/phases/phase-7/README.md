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
    // Repository operations
    Task<GitRepository> OpenRepositoryAsync(string path);
    Task<IEnumerable<GitFile>> GetStatusAsync(CancellationToken ct);
    Task<IEnumerable<GitCommit>> GetLogAsync(int count, CancellationToken ct);
    
    // File operations
    Task StageAsync(string filePath, CancellationToken ct);
    Task UnstageAsync(string filePath, CancellationToken ct);
    Task<GitCommit> CommitAsync(string message, CancellationToken ct);
    
    // Branch operations
    Task<IEnumerable<GitBranch>> GetBranchesAsync(CancellationToken ct);
    Task CheckoutAsync(string branchName, CancellationToken ct);
}

public record GitRepository(string Path, string CurrentBranch);
public record GitFile(string Path, GitFileStatus Status);  // Modified, Staged, Untracked
public record GitCommit(string Hash, string Message, DateTimeOffset Author, string AuthorEmail);
public record GitBranch(string Name, bool IsCurrent, bool IsRemote);
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
