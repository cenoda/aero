# Phase 7: Git Integration — Consolidated Review

> **Reviewer:** Cline (automated)
> **Date:** 2026-06-22
> **Build:** `dotnet build src/aero.csproj` — **✅ Pass** (0 warnings, 0 errors)
> **Tests:** `dotnet test tests` — **✅ Pass** (360 passed, 0 skipped, 0 failed)

---

## 1. Exit-Condition Checklist (from PHASES.md)

| Exit Condition | Status | Notes |
|----------------|--------|-------|
| Git panel shows staged/unstaged changes | ✅ | `GitPanelView` with dual-column Staged/Unstaged `ItemsControl` |
| Diff viewer shows inline +/- gutters | ⚠️ | Diff view shows colored backgrounds but **no `+`/`-` gutter column or line numbers** — just raw patch text with colored backgrounds |
| Can stage, unstage, and commit from UI | ⚠️ | Commit button + message box present. **No per-file Stage/Unstage buttons** — the `GitPanelView` only has Diff (click) and Commit/Refresh buttons. `StageAllCommand`/`UnstageAllCommand` exist in the ViewModel but have **no corresponding UI buttons** |
| Status bar shows current branch | ✅ | `MainWindow.axaml` binds `GitViewModel.CurrentBranch` with green foreground, visible when `HasGitRepository` is true |
| Modified files show indicators in tabs and file tree | ✅ | `EditorTabViewModel.GitStatusGlyph`, `FileExplorerNodeViewModel.GitStatusGlyph` — both wired via `GitStatusChanged` subscription |

---

## 2. Architecture & Design

### ✅ Strengths

1. **Interface-first design** — `IGitService` is clean, well-documented, and abstraction-ready. A `GitCliService` or `OctokitService` can be added without touching core code.

2. **Thread safety** — All `Repository` access serialized via `SemaphoreSlim(1,1)`. Every public method acquires/releases properly with `try/finally`.

3. **Factory caching** — `GitServiceFactory.Detect()` caches the `IGitService` instance per workspace path and disposes old services when switching workspaces. Implements `IDisposable`.

4. **Graceful degradation** — `GitServiceUnavailableException` wrapping for native library failures. Factory returns `null` when no `.git` exists. ViewModels handle `null` service gracefully.

5. **MVVM purity** — Git ViewModels have zero View references. Cross-component communication via `MessageBus` with typed records (`GitStatusChanged`, `GitBranchChanged`, `GitDiffRequested`, `GitRepositoryChanged`).

6. **DI registration** — All services registered as singletons in `App.axaml.cs::BuildServices()`. `GitViewModel` eagerly resolved for MessageBus subscription before first folder open.

7. **Coarse-grained debouncing** — 1-second cooldown on Git status refresh prevents excessive rescans during builds.

8. **Models** — Clean, well-documented C# records. Separation of index-side vs workdir-side status is correct.

9. **Diff line cap** — 10,000-line cap prevents UI freeze on large generated files.

10. **Native library safety** — Constructor catches `DllNotFoundException`, `BadImageFormatException`, `TypeInitializationException` and wraps in `GitServiceUnavailableException`.

### ⚠️ Issues Found

#### P1: GitPanelView Missing Stage/Unstage UI (Medium)

**Location:** `src/Views/GitPanelView.axaml`

The ViewModel defines `StageFileCommand`, `UnstageFileCommand`, `StageAllCommand`, and `UnstageAllCommand`, but the XAML has **no buttons** to invoke them. The Git panel shows a diff viewer when clicking a file, but users cannot stage or unstage individual files.

**Impact:** Users can only commit pre-staged files. No way to stage/unstage from the UI.

**Fix:** Add per-file stage (→) and unstage (←) buttons next to each file entry, and "Stage All" / "Unstage All" buttons in the column headers.

#### P2: GetBranchesAsync Test is a No-Op (Medium)

**Location:** `tests/Services/Git/LibGit2SharpServiceTests.cs:144-150`

```csharp
[Fact]
public async Task GetBranchesAsync_AfterCommit_ReturnsCurrentBranch()
{
    // Branch detection is tested indirectly via GetRepositoryInfoAsync...
    await Task.CompletedTask;
}
```

The TOFIX R2.1 says this was fixed by switching to `repo.Refs.FromGlob`, but the test never actually exercises this code. It should create a repo, call `GetBranchesAsync`, and assert the branch is returned.

#### P3: Diff View Lacks Line Numbers and Gutter Markers (Medium)

**Location:** `src/Views/GitDiffView.axaml`, `src/ViewModels/GitDiffViewModel.cs`

The diff view renders patch text with colored backgrounds (green for additions, red for deletions) but does not show:
- Line numbers
- `+`/`-` gutter column (the raw patch text includes them, but they're not visually separated)
- Hunk header styling (headers look the same as content lines)

The PHASES.md roadmap says "Diff viewer — inline diff with +/- gutter."

#### P4: Commit Author is Hardcoded (Low)

**Location:** `src/ViewModels/GitViewModel.cs:279-280`

```csharp
var authorName = Environment.UserName;
var authorEmail = $"{authorName}@localhost";
```

This generates `username@localhost` which is not useful for real git commits. Should read from `git config user.name`/`user.email` via `LibGit2SharpService`, or from a future settings page.

#### P5: GitPanelView Has No Branch Checkout UI (Low)

**Location:** `src/Views/GitPanelView.axaml`

The ViewModel defines `Branches` collection and `CheckoutBranchCommand`, but the panel XAML does not render branches or provide a checkout dropdown. Branch switching is only available via the status bar (display only).

---

## 3. TOFIX Reconciliation

### R1.3 — Git Status Refresh Cooldown

**TOFIX says:** `[ ] Open`
**Actual status:** Partially fixed — `GitViewModel` has a 1-second cooldown (`_refreshCooldown = TimeSpan.FromSeconds(1)`), but the TOFIX entry was not updated to reflect this. The persistent-checks section marks it `[x]`.

**Action:** Update TOFIX R1.3 status to `[x] Fixed — 1-second cooldown in GitViewModel`.

### R1.9 — FolderChanged Change Kind

**TOFIX says:** `[ ] Deferred (documented limitation)`
**Status:** Acceptable for Phase 7. No action needed.

---

## 4. Code Quality Observations

### Minor Issues

1. **Inconsistent indentation** — Several lines in `ShellViewModel.cs` (lines 78, 121, 156, 648) have inconsistent leading whitespace, suggesting merge artifacts or auto-format drift. Not a functional issue but hurts readability.

2. **GitPanelView click-to-diff binding** — The `$parent[ItemsControl].((vm:GitViewModel)DataContext).DiffCommand` binding pattern works but is fragile — if the visual tree changes, the binding breaks silently. Consider using a `Name` reference or direct DataContext access.

3. **`GitServiceFactory.Dispose()` uses `_lock.Wait()` (synchronous)** — This blocks the calling thread. Since `Dispose` is typically called from DI container teardown (not async context), this is acceptable but worth noting.

4. **Test cleanup in `GitServiceFactoryTests`** — Uses `Directory.Delete(recursive: true)` in Dispose, which can fail on Windows if files are still locked. The `try/catch` handles this, but it's a known cross-platform concern.

---

## 5. Test Coverage Summary

| Component | Tests | Coverage |
|-----------|-------|----------|
| `GitServiceFactory` | 6 tests | ✅ Null, empty, fake repo, non-existent path, dispose |
| `LibGit2SharpService` | 5 tests | ✅ GetRepositoryInfo, GetStatus (untracked + staged), GetFileDiff, Constructor error. **Missing:** Stage/Unstage round-trip, Commit, Checkout, GetBranches |
| `GitDiffViewModel` | 6 tests | ✅ Line flattening, colors |
| `GitStatusIndicatorTests` | 5 tests | ✅ Staged/modified glyphs, precedence, clearing |
| **Total Git-related** | **22 tests** | Good coverage for core paths; missing stage/unstage/commit integration |

---

## 6. Checklist: Phase 7 Items

- [x] **IGitService interface** — abstraction with repository, file, branch operations
- [x] **LibGit2SharpService** — implements IGitService with thread safety
- [x] **GitServiceFactory** — auto-detect .git directory, caches instance
- [x] **Git panel** — staged/unstaged changes list (view needs stage/unstage buttons)
- [x] **Diff viewer** — colored backgrounds (needs line numbers and gutter)
- [x] **Commit UI** — message box and commit button (author info is hardcoded)
- [x] **Branch indicator in status bar** — green text in MainWindow status bar
- [x] **File modified indicator** — in editor tabs and file tree via MessageBus

---

## 7. Recommendations for Phase 8 Polish

1. Add Stage/Unstage buttons to GitPanelView (P1 — address before Phase 8)
2. Enhance diff view with line numbers and gutter markers (P2)
3. Wire branch checkout UI into GitPanelView or a dropdown (P3)
4. Read git config for commit author (P4)
5. Add remaining integration tests: stage round-trip, commit, checkout (P2)

---

*Phase 7 is functionally complete and the build/tests pass cleanly. The main gaps are in the Git panel UI (missing stage/unstage buttons) and diff view polish (no line numbers/gutter). These are usability improvements that should be addressed before moving to Phase 8.*