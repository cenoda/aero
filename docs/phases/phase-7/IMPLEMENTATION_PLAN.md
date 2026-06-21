# Phase 7 — Implementation Plan

> **Phase:** 7 — Git Integration
> **Date:** 2026-06-21
> **Status:** Ready for implementation

---

## 1. Goal

Add Git support so the user can see what changed, stage/unstage files, write a
commit message, and commit — all from inside the IDE. Surface the current branch
in the status bar and show modified-file indicators in the editor tabs and file
tree.

Key deliverables:

- `IGitService` — abstraction over Git operations (interface-first, per `AGENTS.md` §4)
- `LibGit2SharpService` — the **only** concrete implementation this phase
- `GitServiceFactory` — auto-detects `.git` directory from workspace root
- `GitViewModel` — drives the Git sidebar panel (staged/unstaged file list)
- `GitDiffView` — inline diff viewer showing added/removed lines
- Commit UI — message input + stage/unstage + commit button
- Status bar branch indicator
- Modified-file glyph in editor tabs and file explorer tree
- `ManualTestPhase7.sh` — end-to-end smoke test

This phase is intentionally **basic**: status, stage, unstage, commit, branch display,
and diff viewing. Advanced Git operations (rebase, cherry-pick, stash, merge, etc.)
are **out of scope** (see §10).

---

## 2. Entry Gate

Phase 6 is complete and the gate holds:

| Gate | Verification | Result |
|------|--------------|--------|
| Phase 6 checklist complete | `docs/roadmap/PHASES.md` Phase 6 items all `[x]` | ✅ |
| Build passes | `dotnet build src/aero.csproj` | ✅ 0 errors |
| Tests pass | `dotnet test tests` | ✅ **337/337** |
| .NET SDK present | `dotnet --version` | ✅ 9.0.117 |

### Seams Phase 7 builds on (each verified in `src/`)

- **`IMessageBus`** (`src/Core/IMessageBus.cs`): record-based pub/sub. `FolderOpened`
  already triggers the file explorer refresh — the same pattern will trigger Git status
  refresh when a folder is opened.
- **`src/Core/Messages.cs`**: already declares `FolderOpened(string Path)` and
  `FolderChanged(string Path)`. We add new Git-specific messages here.
- **`DiagnosticStore`** / **`ProblemsViewModel`**: established pattern for a store
  that publishes updates via `DiagnosticsUpdated`. Git status will follow the same
  store → ViewModel → View pipeline.
- **`ShellViewModel`** (`src/ViewModels/ShellViewModel.cs`): owns sidebar panel
  ViewModels, status bar text, and the main `KeyBindings`. Git panel and branch
  label are wired here.
- **`FileExplorerViewModel`** (`src/ViewModels/FileExplorerViewModel.cs`): builds
  `FileExplorerNodeViewModel` tree. Each node already has `Name`, `FullPath`,
  `IsDirectory`. We add a `GitStatus` property for the gutter badge.
- **`EditorTabViewModel`** (`src/ViewModels/EditorTabViewModel.cs`): each tab shows
  `Title` and `IsDirty`. We add a `GitStatus` indicator (modified dot, staged dot).
- **`OutputViewModel`** (`src/ViewModels/OutputViewModel.cs`): can stream externally
  produced output lines. If we need to show raw `git` command output, this is available.
- **`IProjectLoader`** (`src/Services/ProjectLoader.cs`): `DetectProjectKind` can be
  extended to detect `.git` directory presence.
- **DI** (`src/App.axaml.cs`): all services are singletons. Git services follow the
  same pattern. Eager-resolve any service that subscribes to `FolderOpened`.

---

## 3. Scope

### In Scope

1. `IGitService` interface + `GitStatus`, `GitFile`, `GitBranch`, `GitCommit`, `GitDiff`
   models
2. `LibGit2SharpService` — implements `IGitService` using LibGit2Sharp NuGet package
3. `GitServiceFactory` — detects `.git` directory from workspace root
4. `GitViewModel` — staged/unstaged/untracked file list with stage/unstage commands
5. `GitCommitViewModel` — message input + commit button
6. `GitDiffView` — inline diff (side-by-side or unified, showing +/-)
7. `GitPanelView` — sidebar panel combining file list + commit UI + diff
8. Branch label in the status bar
9. Modified-file glyph in editor tabs (dot for dirty, colored dot for staged)
10. Modified-file glyph in file explorer tree
11. Refresh Git status on `FolderOpened` and `FolderChanged`
12. Unit tests for `LibGit2SharpService` logic (mock-friendly via interface)
13. Unit tests for `GitViewModel` (using `NSubstitute` mock of `IGitService`)
14. Integration test for `GitServiceFactory` (temp-dir `.git init`)
15. `ManualTestPhase7.sh`

### Out of Scope (see §10)

- Git stash, rebase, cherry-pick, merge, revert, reset, bisect, reflog
- Git remote operations (fetch, pull, push) — future phase
- Git blame viewer — future phase
- Git hooks management — future phase
- Git submodule, worktree, sparse-checkout — future phase
- Branch creation/deletion/renaming UI (only display + checkout current)
- Merge conflict resolution UI
- Interactive rebase UI
- Git credential manager

---

## 4. Dependency Decision

### New NuGet Packages

| Package | Version | Why |
|---------|---------|-----|
| **LibGit2Sharp** | `0.31.*` | Typed Git objects — status, diff, log, branches, commits. Replaces fragile CLI parsing. |
| **DiffPlex** | `1.*` | Unified/inline diff algorithm for the diff viewer. |

Both are well-maintained, MIT-licensed, and standard choices for .NET Git tooling.

### Existing Packages Reused

- **CliWrap** (`3.*`): available if we need a fallback CLI path, but LibGit2Sharp
  handles everything in-process.
- **ReactiveUI / DynamicData**: `GitViewModel` uses `ObservableCollection` + reactive
  subscriptions following the existing pattern.
- **Microsoft.Extensions.DependencyInjection**: register `IGitService` as singleton.

---

## 5. Architecture

### 5.1 Git abstraction (`src/Services/Git/`)

```csharp
// src/Services/Git/IGitService.cs
namespace Aero.Services.Git;

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
```

```csharp
// src/Services/Git/GitModels.cs
namespace Aero.Services.Git;

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

### 5.2 `LibGit2SharpService`

Location: `src/Services/Git/LibGit2SharpService.cs`

- Wraps `LibGit2Sharp.Repository` for all operations.
- Opens the repository lazily on first call (or when `FolderOpened` fires).
- `GetStatusAsync`: maps `LibGit2Sharp.StatusEntry` → `GitFileStatus`. Combines
  workspace status and index status into a single unified view.
- `StageAsync` / `UnstageAsync`: calls `Repository.Stage()` / `Repository.Unstage()`.
- `CommitAsync`: creates a `Signature` and calls `Repository.Commit()`.
- `GetBranchesAsync`: maps `Repository.Branches` → `GitBranchInfo`.
- `CheckoutAsync`: calls `Commands.Checkout()`.
- `GetFileDiff`: uses `Repository.Diff.Compare<TreeChanges>()` for the specified file,
  then converts patch hunks to `GitDiffHunk`/`GitDiffLine`.
- `GetLogAsync`: maps `Repository.Log()` → `GitCommitInfo`.
- All methods catch `LibGit2Sharp.GitException` and wrap in a result type (never throw).
- Implements `IDisposable` to close the `Repository` handle.

### 5.3 `GitServiceFactory`

Location: `src/Services/Git/GitServiceFactory.cs`

```csharp
public class GitServiceFactory
{
    /// <summary>
    /// Returns an IGitService if the workspace contains a .git directory,
    /// null otherwise.
    /// </summary>
    public IGitService? Detect(string workspacePath)
    {
        var gitDir = Path.Combine(workspacePath, ".git");
        if (Directory.Exists(gitDir))
            return new LibGit2SharpService(gitDir);
        return null;
    }
}
```

### 5.4 Messages

Added to `src/Core/Messages.cs`:

```csharp
// Git messages
public record GitStatusChanged(IReadOnlyList<GitFileStatus> Files, GitRepositoryInfo RepoInfo);
public record GitBranchChanged(string BranchName);
public record GitDiffRequested(string FilePath);
```

### 5.5 ViewModels

#### `GitViewModel` (`src/ViewModels/GitViewModel.cs`)

- Subscribes to `FolderOpened` → calls `GitServiceFactory.Detect()` → loads status.
- Subscribes to `FolderChanged` → refreshes status.
- `Files`: `ObservableCollection<GitFileViewModel>` (filtered: staged vs unstaged).
- Commands: `StageCommand`, `UnstageCommand`, `RefreshCommand`.
- `CommitCommand`: validates message non-empty, calls `IGitService.CommitAsync()`,
  refreshes status on success.
- `BranchName`: bound to status bar label.
- `DiffCommand`: publishes `GitDiffRequested` when a file is double-clicked.

#### `GitFileViewModel`

- Wraps `GitFileStatus` for display.
- Properties: `DisplayName`, `StatusText` (Modified, Added, etc.), `StatusKind`,
  `IsStaged`, `FilePath`.

### 5.6 Views

#### `GitPanelView` (`src/Views/GitPanelView.axaml`)

Layout (top to bottom):

```
┌─────────────────────────────┐
│  Branch: main  ↻ Refresh    │  ← header row
├─────────────────────────────┤
│  CHANGES (2)                │  ← section header
│  ○ file1.cs    Modified     │  ← unstaged files
│  ● file2.cs    Added        │  ← staged files
│  ○ file3.cs    Untracked    │
├─────────────────────────────┤
│  ┌─────────────────────┐    │
│  │ Commit message...   │    │  ← text input
│  └─────────────────────┘    │
│  [Commit (2 staged)]        │  ← commit button
└─────────────────────────────┘
```

- File list uses `ListBox` with data template showing status icon + name + kind.
- Right-click context menu: Stage / Unstage / Open.
- Commit button disabled when no staged files or message is empty.

#### `GitDiffView` (`src/Views/GitDiffView.axaml`)

- Opens as a new editor tab (reuses the tab infrastructure).
- Shows unified diff with color-coded lines (green additions, red deletions).
- Line numbers on both sides (old/new).
- Uses `DiffPlex` for generating the diff display.

### 5.7 Status Bar Integration

In `ShellViewModel`:
- Add `GitBranch` property (string, e.g. "main").
- Subscribe to `GitStatusChanged` → update `GitBranch`.
- In `MainWindow.axaml` status bar grid: add a `TextBlock` bound to `GitBranch`
  in the existing `*` column (column 2). Show only when a repo is open.

### 5.8 Modified-File Indicators

#### Editor Tabs

- `EditorTabViewModel` gains a `GitStatusIndicator` property (string: "" /
  "● modified" / "● staged").
- Updated when `GitStatusChanged` fires — match by file path.

#### File Explorer Tree

- `FileExplorerNodeViewModel` gains a `GitStatusIndicator` property.
- Updated when `GitStatusChanged` fires — match by full path.
- Displayed as a colored dot or text glyph next to the filename.

---

## 6. Milestones

### M1 — NuGet + Models + Interface

**Goal:** Add LibGit2Sharp + DiffPlex, define `IGitService`, all Git models, and
`GitServiceFactory`.

Files created:
- `src/Services/Git/IGitService.cs`
- `src/Services/Git/GitModels.cs`
- `src/Services/Git/GitServiceFactory.cs`
- `src/aero.csproj` — add LibGit2Sharp + DiffPlex packages

Files modified:
- `src/Core/Messages.cs` — add `GitStatusChanged`, `GitBranchChanged`, `GitDiffRequested`

Tests:
- `tests/Services/GitServiceFactoryTests.cs` — detect `.git` directory, return null
  when absent

### M2 — LibGit2SharpService Implementation

**Goal:** Full `IGitService` implementation using LibGit2Sharp.

Files created:
- `src/Services/Git/LibGit2SharpService.cs`

Tests:
- `tests/Services/LibGit2SharpServiceTests.cs` — integration tests using temp repos
  (`git init`, add files, commit, check status, stage/unstage, diff)

### M3 — GitViewModel + GitPanelView

**Goal:** Wire up the Git panel in the sidebar with file list, stage/unstage, commit.

Files created:
- `src/ViewModels/GitViewModel.cs`
- `src/ViewModels/GitFileViewModel.cs`
- `src/Views/GitPanelView.axaml` + `GitPanelView.axaml.cs`

Files modified:
- `src/ViewModels/ShellViewModel.cs` — add `GitViewModel`, `GitBranch`, wire
  `FolderOpened`/`FolderChanged` → Git status refresh
- `src/Views/MainWindow.axaml` — add Git panel to sidebar, branch label to status bar
- `src/App.axaml.cs` — register `GitServiceFactory`, `GitViewModel` in DI

Tests:
- `tests/ViewModels/GitViewModelTests.cs` — mock `IGitService`, test stage/unstage/commit
  commands

### M4 — Diff Viewer

**Goal:** Show inline diff when a file is selected in the Git panel.

Files created:
- `src/Views/GitDiffView.axaml` + `GitDiffView.axaml.cs`
- `src/ViewModels/GitDiffViewModel.cs`

Files modified:
- `src/ViewModels/GitViewModel.cs` — wire `GitDiffRequested` → open diff tab
- `src/ViewModels/EditorViewModel.cs` — handle `GitDiffRequested` to open diff as a tab
- `src/ViewModels/EditorTabViewModel.cs` — add `GitStatusIndicator`

Tests:
- `tests/ViewModels/GitDiffViewModelTests.cs` — mock diff data, verify rendering model

### M5 — Modified-File Indicators + Polish

**Goal:** Git status badges in editor tabs and file explorer tree. Polish UI.

Files modified:
- `src/ViewModels/EditorTabViewModel.cs` — `GitStatusIndicator` bound to Git status
- `src/ViewModels/FileExplorerNodeViewModel.cs` — `GitStatusIndicator` bound to Git status
- `src/ViewModels/FileExplorerViewModel.cs` — subscribe to `GitStatusChanged`, update nodes
- `src/Views/EditorView.axaml` — display GitStatusIndicator in tab header
- `src/Views/FileExplorerView.axaml` — display GitStatusIndicator next to filename
- `src/Views/GitPanelView.axaml` — final polish: icons, spacing, commit button styling

### M6 — Tests + Manual Test + Docs

**Goal:** All tests green, manual test script, documentation updated.

Files created:
- `manual_test/manual_test_phase7.sh`

Files modified:
- `docs/roadmap/PHASES.md` — mark completed items `[x]`
- `docs/LIBRARIES.md` — confirm LibGit2Sharp + DiffPlex entries (already present)

Tests: final `dotnet test tests` — target **370+** green.

---

## 7. Key Design Decisions

### D1: LibGit2Sharp over Git CLI

LibGit2Sharp gives typed objects and in-process execution — no stdout parsing,
no subprocess overhead, no platform-specific path issues. The only trade-off is
a native dependency (`libgit2`), but LibGit2Sharp bundles it per-RID.

### D2: DiffPlex for Diff Rendering

DiffPlex is a pure C# diff algorithm. It produces structured diff output that
we render ourselves in the diff view. This avoids pulling in a full WPF/Avalonia
diff control and keeps the rendering consistent with our editor's visual style.

### D3: Unified Diff (not Side-by-Side)

For Phase 7, the diff viewer shows **unified** diff format (additions/deletions
inline). Side-by-side is more complex to implement in AvaloniaEdit's single-column
editor model and can be added later.

### D4: Factory Returns Null (No Repo)

`GitServiceFactory.Detect()` returns `null` when no `.git` directory exists. The
`GitViewModel` handles this gracefully by hiding the Git panel / showing "Not a
Git repository". No error, no exception — the panel simply doesn't activate.

### D5: Git Status as a Store (Like DiagnosticStore)

`GitViewModel` acts as the Git status store. It owns the file list and publishes
updates via `GitStatusChanged`. Other components (editor tabs, file explorer)
subscribe to this message — same pattern as `DiagnosticStore` → `ProblemsViewModel`.

---

## 8. Testing Strategy

| Layer | What to Test | How |
|-------|-------------|-----|
| `GitServiceFactory` | `.git` detection | Temp dir with/without `.git` |
| `LibGit2SharpService` | Status, stage, unstage, commit, diff, log | Temp git repos (`git init` + commits) |
| `GitViewModel` | Commands, status refresh, commit flow | `NSubstitute` mock of `IGitService` |
| `GitDiffViewModel` | Diff data → display model | Unit test with sample diff |
| Integration | Full flow: folder open → status → stage → commit | `manual_test_phase7.sh` |

### Test Count Projection

| Existing | New Tests | Projected Total |
|----------|-----------|-----------------|
| 337 | ~35-40 | **~375** |

---

## 9. Risk Register

| ID | Risk | Impact | Mitigation |
|----|------|--------|------------|
| R1 | LibGit2Sharp native binary missing on some Linux distros | High | LibGit2Sharp bundles `libgit2` per-RID via NuGet. Test on the target platform early in M2. |
| R2 | Large repo status is slow | Medium | `GetStatusAsync` is already async and off-UI-thread. For very large repos, we can add cancellation support. Defer optimization. |
| R3 | Diff viewer performance on large files | Low | DiffPlex handles large files well. Cap diff to first N lines if needed. |
| R4 | Concurrent Git operations (status + commit) | Medium | LibGit2Sharp `Repository` is not thread-safe. Use `SemaphoreSlim(1)` to serialize access. |
| R5 | Tab integration for diff viewer | Medium | Reuse existing `EditorViewModel.OpenDocumentAsync` pattern but with a synthetic "diff document". Alternatively, open a dedicated diff tab. |

---

## 10. Limitations

This phase is intentionally scoped to the **minimum viable Git integration**.
The following are explicitly deferred:

- Branch creation/deletion/renaming UI
- Remote operations (fetch, pull, push)
- Stash management
- Interactive rebase / merge / cherry-pick
- Conflict resolution UI
- Blame viewer
- Git hooks management
- Git submodule / worktree / sparse-checkout support
- Bisect / reflog / notes
- Credential manager integration
- Git alias / config management

These can be added in future phases as the IDE matures.

---

## 11. File Layout (New Files)

```
src/
  Services/
    Git/
      IGitService.cs              ← M1
      GitModels.cs                ← M1
      GitServiceFactory.cs        ← M1
      LibGit2SharpService.cs      ← M2
  ViewModels/
    GitViewModel.cs               ← M3
    GitFileViewModel.cs           ← M3
    GitDiffViewModel.cs           ← M4
  Views/
    GitPanelView.axaml            ← M3
    GitPanelView.axaml.cs         ← M3
    GitDiffView.axaml             ← M4
    GitDiffView.axaml.cs          ← M4
tests/
  Services/
    GitServiceFactoryTests.cs     ← M1
    LibGit2SharpServiceTests.cs   ← M2
  ViewModels/
    GitViewModelTests.cs          ← M3
    GitDiffViewModelTests.cs      ← M4
```

---

## 12. Commit Plan

Following the "one concern per change" rule (§2 of `AGENTS.md`):

1. `git: add LibGit2Sharp + DiffPlex, IGitService interface, GitModels, GitServiceFactory`
2. `git: implement LibGit2SharpService with status, stage, unstage, commit, diff`
3. `git: add GitViewModel, GitFileViewModel, GitPanelView, commit UI`
4. `git: add GitDiffView and GitDiffViewModel for inline diff`
5. `git: add GitStatusIndicator to editor tabs and file explorer tree`
6. `git: add branch label to status bar, polish Git panel UI`
7. `git: add unit and integration tests for Phase 7`
8. `git: add manual_test_phase7.sh, update PHASES.md checklist`
