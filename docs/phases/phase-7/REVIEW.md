# Phase 7: Git Integration — Comprehensive Review

> **Reviewer:** Cline (automated)
> **Date:** 2026-06-22
> **Build:** `dotnet build src/aero.csproj` — **✅ Pass** (0 warnings, 0 errors)
> **Tests:** `dotnet test tests` — **✅ Pass** (360 passed, 0 skipped, 0 failed)

---

## 0. Important Note on Previous Review

The original REVIEW.md (Round 1) contained **stale findings** that were already fixed
at the time of writing. This review is a complete re-examination of the actual source code.

| Original Claim | Actual State |
|----------------|-------------|
| P1: "No per-file Stage/Unstage buttons" | **Wrong** — `GitPanelView.axaml` lines 40–42 (←) and 74–76 (→) exist |
| P1: "No Stage All / Unstage All buttons" | **Wrong** — Lines 31–32 and 65–66 exist |
| P3: "Diff view lacks line numbers and gutter markers" | **Wrong** — `GitDiffView.axaml` lines 16–28 have gutter + old/new line numbers |
| P5: "No branch checkout UI" | **Wrong** — Lines 108–122 have ComboBox + Checkout button |

---

## 1. Exit-Condition Checklist (from PHASES.md)

| Exit Condition | Status | Notes |
|----------------|--------|-------|
| Git panel shows staged/unstaged changes | ✅ | Dual-column Staged/Unstaged `ItemsControl` with per-file and bulk buttons |
| Diff viewer shows inline +/- gutters | ⚠️ | Gutter column exists but **text is invisible** (FG matches BG color); line numbers are **always 0** |
| Can stage, unstage, and commit from UI | ✅ | Per-file ←/→ buttons, Stage All / Unstage All, Commit button with message box |
| Status bar shows current branch | ✅ | `MainWindow.axaml` binds `GitViewModel.CurrentBranch` with green foreground |
| Modified files show indicators in tabs and file tree | ✅ | `EditorTabViewModel.GitStatusGlyph`, `FileExplorerNodeViewModel.GitStatusGlyph` via `GitStatusChanged` |

---

## 2. Architecture & Design

### ✅ Strengths

1. **Interface-first design** — `IGitService` is clean, well-documented, and abstraction-ready.
   A `GitCliService` or `OctokitService` can be added without touching core code.

2. **Thread safety** — All `Repository` access serialized via `SemaphoreSlim(1,1)`.
   Every public method acquires/releases properly with `try/finally`.

3. **Factory caching** — `GitServiceFactory.Detect()` caches the `IGitService` instance
   per workspace path and disposes old services when switching workspaces. Implements `IDisposable`.

4. **Graceful degradation** — `GitServiceUnavailableException` wrapping for native library failures.
   Factory returns `null` when no `.git` exists. ViewModels handle `null` service gracefully.

5. **MVVM purity** — Git ViewModels have zero View references. Cross-component communication
   via `MessageBus` with typed records (`GitStatusChanged`, `GitBranchChanged`,
   `GitDiffRequested`, `GitRepositoryChanged`).

6. **DI registration** — All services registered as singletons in `App.axaml.cs::BuildServices()`.
   `GitViewModel` eagerly resolved for MessageBus subscription before first folder open.

7. **Coarse-grained debouncing** — 1-second cooldown on Git status refresh prevents
   excessive rescans during builds.

8. **Models** — Clean, well-documented C# records. Separation of index-side vs workdir-side
   status is correct.

9. **Diff line cap** — 10,000-line cap prevents UI freeze on large generated files.

10. **Native library safety** — Constructor catches `DllNotFoundException`,
    `BadImageFormatException`, `TypeInitializationException` and wraps in
    `GitServiceUnavailableException`.

11. **Git config for commit author** — `GetConfigAsync` reads `user.name`/`user.email`
    from git config, with fallback to `Environment.UserName`.

12. **File tree and editor tab indicators** — Both `FileExplorerViewModel` and
    `EditorViewModel` subscribe to `GitStatusChanged` and `GitRepositoryChanged`,
    updating `GitStatusGlyph` correctly with staged-over-unstaged precedence.

13. **Comprehensive MessageBus integration** — Four typed Git messages for clean
    cross-component communication.

---

### ⚠️ Issues Found

#### Issue 1: `GetFileDiffAsync` Always Compares HEAD vs WorkingDirectory *(Medium)*

**Location:** `src/Services/Git/LibGit2SharpService.cs:310–311`

```csharp
var headTip = repo.Head.Tip;
var patch = repo.Diff.Compare<Patch>(headTip?.Tree, DiffTargets.WorkingDirectory);
```

This always produces the diff from HEAD to the working directory, regardless of whether
the file is staged or unstaged. This means:
- **Staged files** show the TOTAL diff from HEAD (staged + unstaged changes), not just
  the staged portion.
- **Unstaged files** show the same diff, making the "Staged" and "Unstaged" diff views
  identical for files that have both staged and unstaged modifications.

**Expected behavior:**
- Unstaged diff: compare `Index` vs `WorkingDirectory`
- Staged diff: compare `HEAD` vs `Index`

**Impact:** Users see incorrect diffs when files have partial staging.

**Fix:** Add a `diffType` parameter (or overload) to `GetFileDiffAsync` that controls
the comparison, and update `GitViewModel.DiffCommand` to pass the appropriate type
based on which column the file is in.

---

#### Issue 2: Diff Gutter Text Is Invisible *(Medium)*

**Location:** `src/Views/GitDiffView.axaml:18`

```xml
<TextBlock Grid.Column="0" Text="{Binding Gutter}" Width="20"
          FontFamily="Consolas, Menlo, monospace" FontSize="12"
          FontWeight="Bold" Foreground="{Binding LineBackground}"/>
```

The gutter text `Foreground` is bound to `LineBackground`, which means:
- Addition lines: green text on green background → invisible
- Deletion lines: red text on red background → invisible
- Header lines: blue text on blue background → invisible

**Fix:** Use a contrasting foreground for gutter text:
```xml
Foreground="{Binding GutterForeground}"
```
Where `GutterForeground` is a new property on `GitDiffLineViewModel` that returns
the appropriate color (e.g., dark green for additions, dark red for deletions, or
the system foreground for headers).

---

#### Issue 3: Diff Hunk Metadata Is All Zeros *(Medium)*

**Location:** `src/Services/Git/LibGit2SharpService.cs:354`

```csharp
hunks.Add(new GitDiffHunk(0, 0, 0, 0, hunkLines));
```

The hunk's `OldStart`, `OldCount`, `NewStart`, `NewCount` are all set to 0 instead of
being parsed from the `@@ -x,y +a,b @@` header line. This causes:
- `GitDiffViewModel` line numbering starts from 0, not from actual file positions
- The `@@ -x,y +a,b @@` header is included as a content line but its values aren't
  parsed to initialize line counters

**Fix:** Parse the hunk header line to extract `OldStart`, `OldCount`, `NewStart`,
`NewCount` and pass them to `GitDiffHunk` constructor. This gives `GitDiffViewModel`
correct starting line numbers.

---

#### Issue 4: `GitServiceFactory.Detect()` Is Not Thread-Safe *(Medium)*

**Location:** `src/Services/Git/GitServiceFactory.cs:23–68`

The `Detect()` method reads and writes `_cachedService` and `_cachedWorkspacePath`
without acquiring the semaphore lock. If called from multiple threads simultaneously
(e.g., two `FolderOpened` messages in quick succession), the cache could be corrupted.

The `Dispose()` method correctly uses `_lock`, but `Detect()` does not.

**Fix:** Acquire `_lock` at the start of `Detect()` (use `Wait()` since it's
typically called from the UI thread, or convert to `async` with `WaitAsync`).

---

#### Issue 5: `GitViewModel.Dispose()` Incorrectly Owns Factory Disposal *(Medium)*

**Location:** `src/ViewModels/GitViewModel.cs:350`

```csharp
_factory.Dispose();
```

`GitServiceFactory` is registered as a DI singleton in `App.axaml.cs`. The DI container
owns its lifecycle and will dispose it when the container is disposed (via
`OnDesktopExit`). `GitViewModel` should NOT dispose the factory — it's a DI
ownership violation.

While `GitServiceFactory.Dispose()` handles double-dispose via `_disposed` check,
this is architecturally incorrect and could cause issues if the factory is shared
with other consumers.

**Fix:** Remove `_factory.Dispose()` from `GitViewModel.Dispose()`. Let the DI
container own the factory lifecycle.

---

#### Issue 6: Missing Integration Tests for Stage/Unstage/Commit *(Low)*

**Location:** `tests/Services/Git/LibGit2SharpServiceTests.cs`

The tests cover: `GetRepositoryInfoAsync`, `GetStatusAsync` (untracked + staged),
`GetFileDiffAsync`, `GetBranchesAsync`, and constructor error handling.

**Missing tests:**
- `StageAsync` → `GetStatusAsync` round-trip (verify file moves to staged)
- `UnstageAsync` → `GetStatusAsync` round-trip (verify file moves to unstaged)
- `CommitAsync` → verify commit SHA returned, status becomes clean
- `CheckoutAsync` → verify branch changes
- Concurrency test (two concurrent `GetStatusAsync` calls)

---

#### Issue 7: `CheckoutAsync` Uses String Matching on Exception Message *(Low)*

**Location:** `src/Services/Git/LibGit2SharpService.cs:281`

```csharp
catch (Exception ex) when (ex.Message.Contains("conflict") || ex.Message.Contains("Your local changes"))
```

This is fragile — LibGit2Sharp message text could change between versions or be
localized. Better to catch `CheckoutConflictException` specifically, or at minimum
use `LibGit2Sharp.GitException` as the base type.

---

#### Issue 8: `StageAllAsync` / `UnstageAllAsync` Do N+1 Refreshes *(Low)*

**Location:** `src/ViewModels/GitViewModel.cs:230–251`

```csharp
foreach (var file in UnstagedChanges.ToList())
{
    await StageFileAsync(file.FilePath);  // Each call triggers RefreshStatusInternalAsync()
}
```

Each individual `StageFileAsync`/`UnstageFileAsync` call triggers a full status refresh.
For 10 files, this means 10 redundant refreshes.

**Fix:** Call `StageAsync`/`UnstageAsync` in a loop, then call `RefreshStatusInternalAsync()`
once at the end.

---

#### Issue 9: `_lastRefresh` Uses Non-Monotonic Clock *(Low)*

**Location:** `src/ViewModels/GitViewModel.cs:37`

```csharp
private DateTime _lastRefresh = DateTime.MinValue;
```

`DateTime.UtcNow` is not guaranteed monotonic. System clock adjustments (NTP, DST)
could cause the cooldown to be skipped or extended unexpectedly.

**Fix:** Use `Environment.TickCount64` (monotonic, millisecond resolution) instead.

---

#### Issue 10: Diff Content Lines Are All Bold *(Low)*

**Location:** `src/Views/GitDiffView.axaml:32`

```xml
<TextBlock Grid.Column="3" Text="{Binding Content}" FontWeight="Bold"/>
```

All content lines (context, addition, deletion) are rendered in bold. This reduces
readability. Typically only additions/deletions are visually emphasized, while context
lines use regular weight.

---

## 3. TOFIX Reconciliation

### R1.1 — LibGit2Sharp Repository Thread Safety
**Status:** [x] Fixed — `SemaphoreSlim(1,1)` in `LibGit2SharpService`.

### R1.2 — Native Library Load Failure
**Status:** [x] Fixed — Constructor catches and wraps in `GitServiceUnavailableException`.

### R1.3 — Git Status Refresh Cooldown
**Status:** [x] Fixed — 1-second cooldown in `GitViewModel._refreshCooldown`.

### R1.4 — Factory Caching
**Status:** [x] Fixed — caching implemented in `GitServiceFactory.Detect`.

### R1.5 — UI Thread Blocking
**Status:** [x] Fixed — all commands are async via `ReactiveCommand`.

### R1.6 — Large Diff Cap
**Status:** [x] Fixed — 10,000-line cap in `GetFileDiffAsync`.

### R1.7 — Checkout Conflict Handling
**Status:** [x] Fixed — error handling in `GitViewModel.CheckoutAsync`.

### R1.8 — DiffPlex Line Numbering
**Status:** [x] Fixed — documented in `GitDiffViewModel`. However, the actual line numbers
are broken due to Issue 3 (hunk metadata is all zeros).

### R1.9 — FolderChanged Change Kind
**Status:** [ ] Deferred — acceptable for Phase 7.

### R1.10 — Integration Tests
**Status:** [x] Fixed — `LibGit2SharpServiceTests.cs` created. Missing tests noted in Issue 6.

### R1.11 — DI Registration
**Status:** [x] Fixed — registered in `App.axaml.cs`.

### R2.1 — `repo.Branches` Hang
**Status:** [x] Fixed — `GetBranchesAsync` reads refs directly from filesystem.

---

## 4. Test Coverage Summary

| Component | Tests | Coverage |
|-----------|-------|----------|
| `GitServiceFactory` | 6 tests | ✅ Null, empty, fake repo, non-existent path, dispose |
| `LibGit2SharpService` | 6 tests | ✅ GetRepositoryInfo, GetStatus (untracked + staged), GetBranches, GetFileDiff, Constructor error. **Missing:** Stage/Unstage round-trip, Commit, Checkout, concurrency |
| `GitDiffViewModel` | 6 tests | ✅ Line flattening, colors, title |
| `GitStatusIndicatorTests` | 5 tests | ✅ Staged/modified glyphs, precedence, clearing, repository close |
| **Total Git-related** | **23 tests** | Good coverage for core paths; missing stage/unstage/commit integration |

---

## 5. Code Quality Observations

### Indentation
Line 204 in `GitViewModel.cs` has inconsistent indentation (missing leading spaces).
This is a cosmetic issue from a merge or auto-format.

### MessageBus Type Aliases
`GitViewModel.cs` uses unusual aliases:
```csharp
using DocMsg = Aero.Core;
```
This works but is non-obvious. Consider using a more descriptive alias or full namespace.

---

## 6. Checklist: Phase 7 Items

- [x] **IGitService interface** — abstraction with repository, file, branch operations
- [x] **LibGit2SharpService** — implements IGitService with thread safety
- [x] **GitServiceFactory** — auto-detect .git directory, caches instance
- [x] **Git panel** — staged/unstaged changes with per-file and bulk stage/unstage buttons
- [x] **Diff viewer** — gutter, line numbers, colored backgrounds (gutter text invisible)
- [x] **Commit UI** — message box, commit button, git config author resolution
- [x] **Branch indicator in status bar** — green text in MainWindow status bar
- [x] **Branch checkout UI** — ComboBox + Checkout button in GitPanelView
- [x] **File modified indicator** — in editor tabs and file tree via MessageBus

---

## 7. Recommendations (Prioritized)

### Must-Fix Before Phase 8

1. **Fix diff gutter foreground** (Issue 2) — Add `GutterForeground` property to `GitDiffLineViewModel`, use contrasting colors
2. **Fix hunk metadata parsing** (Issue 3) — Parse `@@ -x,y +a,b @@` headers to get correct line numbers
3. **Fix `GitServiceFactory.Detect()` thread safety** (Issue 4) — Acquire semaphore lock
4. **Remove factory disposal from GitViewModel** (Issue 5) — Let DI container own lifecycle

### Should-Fix Before Phase 8

5. **Fix diff comparison semantics** (Issue 1) — Add staged vs unstaged diff support
6. **Add missing integration tests** (Issue 6) — Stage/Unstage/Commit round-trips
7. **Batch stage/unstage operations** (Issue 8) — Single refresh after bulk operation

### Nice-to-Have

8. **Fix exception matching** (Issue 7) — Use typed exceptions instead of string matching
9. **Use monotonic clock** (Issue 9) — `Environment.TickCount64` instead of `DateTime.UtcNow`
10. **Fix bold content** (Issue 10) — Regular weight for context lines

---

*Phase 7 is functionally complete with build and tests passing cleanly. The main gaps
are in diff view correctness (invisible gutter text, zero line numbers, HEAD-only
comparison) and a thread-safety issue in the factory. These should be addressed before
moving to Phase 8.*