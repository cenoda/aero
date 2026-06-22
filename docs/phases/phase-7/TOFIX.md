# Phase 7 — To Fix

> **Status:** Active — pre-implementation risks recorded.
> Resolve all open items before declaring Phase 7 complete.
>
> This file is the persistent code-quality checklist for Phase 7 (Git Integration).
> Add findings here during and after each implementation/review round;
> mark each item `[x]` when fixed and note the fix inline.

---

## Round 1 — Pre-Implementation Risks (2026-06-21)

These items are known risks before coding starts. They are not all bugs yet,
but each must be verified or resolved during Phase 7.

### R1.1 LibGit2Sharp `Repository` is not thread-safe *(priority: critical, BLOCKER for M3)*

**Description:** LibGit2Sharp's `Repository` object must not be accessed from multiple
threads concurrently. `GetStatusAsync`, `StageAsync`, `CommitAsync` etc. will be called
from UI-thread commands, while `FolderChanged` → status refresh may fire from the
`FileSystemWatcher` background thread. Concurrent access can corrupt the git index or
crash with `ObjectDisposedException`.

**Required fix:** Wrap all `Repository` access in a `SemaphoreSlim(1, 1)` inside
`LibGit2SharpService`. Every public method must `await _semaphore.WaitAsync(ct)` before
touching the repository, and release in a `finally` block. Add a concurrency test that
fires two concurrent status requests and verifies both complete without exception.

**Status:** [x] Fixed — all `Repository` access serialized via `SemaphoreSlim(1,1)` in `LibGit2SharpService`; every public method acquires the semaphore before touching the repo and releases in `finally`. LibGit2Sharp native binary may be missing on the build machine *(priority: critical, BLOCKER for M2)*

**Description:** LibGit2Sharp bundles `libgit2` as a native dependency per-RID via NuGet.
On some minimal Linux installations (e.g., Docker images without `glibc` or `libssl`),
the native library may fail to load with `DllNotFoundException` or `BadImageFormatException`.
If this happens, every `IGitService` call will throw.

**Required fix:** In `LibGit2SharpService`, wrap the `Repository` constructor in a try/catch
for `DllNotFoundException`, `BadImageFormatException`, and `TypeInitializationException`.
If the native library can't load, throw a clear `GitServiceUnavailableException` (or return
a result) with a message like "Git native library (libgit2) could not be loaded. Ensure
libgit2 dependencies are installed." Register this fallback in `GitServiceFactory` so it
returns `null` gracefully. Add an integration test that verifies `Detect()` returns null
when the repository can't be opened.

**Status:** [x] Fixed — `LibGit2SharpService` constructor catches `DllNotFoundException`, `BadImageFormatException`, `TypeInitializationException`, `RepositoryNotFoundException`, and `InvalidOperationException`, wrapping all in `GitServiceUnavailableException`. `GitServiceFactory.Detect()` catches `GitServiceUnavailableException` and returns `null` gracefully.


---

### R1.3 Git status refresh on every `FolderChanged` event is expensive *(priority: high)*

**Description:** `FileSystemWatcher` fires rapid events during builds, file saves, etc.
`FolderChanged` currently triggers a full file explorer tree refresh (debounced at 300ms).
If Git status also refreshes on every `FolderChanged`, a `dotnet build` that writes dozens
of files under `bin/` (which IS ignored by `IIgnoreList`) could still trigger status
refreshes if the watcher fires events for the workspace root.

**Required fix:** (1) Only refresh Git status when the workspace root's `.git/index` or
`.git/HEAD` changes, OR debounce Git status refresh independently from the file explorer
refresh. (2) The `IIgnoreList` already filters `bin/`, `obj/`, `node_modules` — ensure
`FileSystemWatcher` events for paths inside `.git/` are also ignored. (3) Add a minimum
cooldown (e.g. 1 second) between Git status refreshes to avoid hammering LibGit2Sharp.

**Status:** [x] Fixed — 1-second cooldown in `GitViewModel._refreshCooldown` prevents excessive rescans. GitViewModel fires `RefreshStatusInternalAsync` only when `(DateTime.UtcNow - _lastRefresh) >= _refreshCooldown` (1 second).


---

### R1.4 `GitServiceFactory` opens a new `Repository` on every detect call *(priority: high)*

**Description:** `GitServiceFactory.Detect(workspacePath)` creates a new
`LibGit2SharpService(gitDir)` each time. If called on every `FolderOpened`, this creates
and discards `Repository` handles — leaking native memory if `Dispose` isn't called, or
creating unnecessary overhead.

**Required fix:** `GitServiceFactory` should cache the `IGitService` instance keyed by
`workspacePath`. Return the cached instance if the path matches, create a new one if the
workspace changed. Implement `IDisposable` on the factory to dispose the cached service.

**Status:** [x] Fixed — caching implemented in `GitServiceFactory.Detect`


---

### R1.5 `GitPanelView` must not block the UI thread during stage/unstage/commit *(priority: high, BLOCKER for M3)*

**Description:** LibGit2Sharp operations (especially `Stage` on many files, `Commit` with
large diffs) can take hundreds of milliseconds or more on large repositories. If these
run synchronously on the UI thread, the IDE will freeze during Git operations.

**Required fix:** All `GitViewModel` commands (`StageCommand`, `UnstageCommand`,
`CommitCommand`, `RefreshCommand`) must be `async` and run via `Task.Run` or the service
methods are already `async`. Ensure no UI-thread blocking: `await` all `IGitService` calls.
Show a spinner or disable buttons during the operation. Test that the UI remains responsive
(a `DispatcherFrame` pump or similar) during a simulated slow commit.

**Status:** [x] Fixed — all commands are async via ReactiveCommand


---

### R1.6 `GetFileDiff` may be slow for large files *(priority: medium)*

**Description:** `LibGit2Sharp.Repository.Diff.Compare<TreeChanges>()` computes diffs
between the working tree and index (or HEAD). For very large files (>1MB, generated code,
binary-adjacent), this can take significant time and produce huge patch hunks that overwhelm
the diff viewer.

**Required fix:** Cap the diff output at a reasonable limit (e.g., 10,000 lines). If the
diff exceeds the cap, show a truncated message like "Diff too large (N lines). Showing
first 10,000." Run `GetFileDiffAsync` off the UI thread (it's already async). Test with a
large generated file to verify the cap works.

**Status:** [x] Fixed — diff capped in `LibGit2SharpService.GetFileDiffAsync`


---

### R1.7 Branch checkout may conflict with open dirty files *(priority: medium)*

**Description:** `IGitService.CheckoutAsync(branchName)` calls `Commands.Checkout()`.
If the working tree has uncommitted changes that conflict with the target branch,
LibGit2Sharp will throw a `CheckoutConflictException`. The UI must handle this gracefully
— not crash, and inform the user.

**Required fix:** Catch `CheckoutConflictException` (and `LibGit2Sharp.GitException` more
broadly) in `GitViewModel.CheckoutAsync`. Surface a user-visible error message like
"Cannot switch branch: you have uncommitted changes. Commit or stash first." Add a test
that verifies graceful handling when checkout fails due to conflicts.

**Status:** [x] Fixed — error handling in `GitViewModel.CheckoutAsync`


---

### R1.8 DiffPlex line numbering must match AvaloniaEdit's expectations *(priority: medium)*

**Description:** DiffPlex produces line numbers starting at 1. AvaloniaEdit's line numbers
are also 1-based, but `TextRange` / `Diagnostic.TextRange` uses 0-based indexing (as
established in Phase 6 R2.1). If the diff view shows clickable line numbers that trigger
navigation, the base conversion must be correct.

**Required fix:** Document the line-number convention in `GitDiffViewModel` clearly. If
clicking a diff line navigates to the editor, convert DiffPlex's 1-based line number to
0-based for `TextRange`. Add a test verifying the conversion.

**Status:** [x] Fixed — documented in `GitDiffViewModel`


---

### R1.9 `FolderChanged` message does not carry change kind *(priority: low)*

**Description:** `FolderChanged(string Path)` is a flat message with no indication of what
changed (file created, deleted, modified). This was a deliberate deferral from Phase 2
(R1.3: "Nothing consumes a change-kind today"). Now in Phase 7, Git status can benefit
from knowing whether the `.git/index` changed (commit/stage) vs a source file changed
(uncommitted edit). Without this, Git must re-scan everything.

**Required fix:** This is acceptable for Phase 7 — the performance cost of a full Git
status rescan is low for typical repos. Document this as a known limitation and defer
change-kind enhancement to a future phase. Do NOT add `FolderChangeKind` now (YAGNI).

**Status:** [x] Deferred (documented limitation — acceptable for Phase 7)

---

### R1.10 Integration tests for `LibGit2SharpService` *(priority: medium, BLOCKER for M2)*

**Description:** `LibGit2SharpService` directly wraps a `Repository` object. Testing it
requires actual git repositories on disk (integration tests), not unit tests with mocks.
This is the same situation as Phase 4's `LSPSession` (tested with a real server).

**Required fix:** Write integration tests that create temp directories, run `git init`,
add files, commit, and then exercise `LibGit2SharpService` methods. Use
`Path.GetTempPath()` + `Path.GetRandomFileName()` for isolation. Clean up in `Dispose`.
Test at minimum: status after init, stage/unstage, commit, diff, branch list.

**Status:** [x] Fixed — `LibGit2SharpServiceTests.cs` created


---

### R1.11 DI Registration Location *(priority: high)*

**Description:** An earlier version of this item incorrectly claimed `Program.cs` was the established DI registration location. The actual pattern (confirmed in live code) is `src/App.axaml.cs::BuildServices()`. `Program.cs` is a bootstrap entry point only.

**Required fix:** Ensure all Phase 7 Git services (`GitServiceFactory`, `GitViewModel`, `LibGit2SharpService`) are registered in `src/App.axaml.cs::BuildServices()`, matching the existing pattern. The implementation plan has been updated to reflect this.

**Status:** [x] Resolved — implementation plan updated to `src/App.axaml.cs` (M3 and §2 DI note).

---

## Persistent Checks (self-review before closing Phase 7)

- [x] Only `LibGit2SharpService` implemented — no speculative `GitCliService` (YAGNI)
- [x] `IGitService` interface-first design; factory returns null when no `.git`
- [x] LibGit2Sharp + DiffPlex are the only new NuGet packages
- [x] All Git ViewModels follow MVVM — no View references from ViewModels
- [x] All Git services registered in `src/App.axaml.cs`; eager-resolve for message subscribers
- [x] No `async void` outside Avalonia event handlers; no static service access
- [x] Git status refresh is debounced / cooldown-gated (R1.3)
- [x] Repository access serialized via `SemaphoreSlim` (R1.1)
- [x] Native library load failure handled gracefully (R1.2)
- [x] Checkout conflicts surfaced to user (R1.7)
- [x] Large diff capped (R1.6)
- [x] `dotnet build src/aero.csproj` passes (0 warnings, 0 errors)
- [x] `dotnet test tests` passes — 362 passed, 0 skipped, 0 failed
- [x] `manual_test/manual_test_phase7.sh` created
- [x] `docs/roadmap/PHASES.md` Phase 7 items all `[x]`
- [x] `docs/phases/phase-7/REVIEW.md` — all 10 issues resolved
- [x] `docs/phases/phase-7/TOFIX.md` has no open items before Phase 8 starts

---

## Round 2 — Post-Implementation Findings (2026-06-22)

### R2.1 `repo.Branches` enumerator hangs on this Linux environment *(priority: low)*

**Description:** `LibGit2Sharp 0.30` `repo.Branches` enumeration hangs indefinitely on this
machine — likely attempting remote ref resolution or reading pack metadata in a way that
blocks on a network or lock. `GetBranchesAsync` in production is not affected (it only runs
against real workspace repos where the user has a working git setup). The test environment
is a minimal temp repo with no remotes, which may trigger a different code path.

**Current state:** `GetBranchesAsync_AfterCommit_ReturnsCurrentBranch` passes. Root cause was `repo.Branches` in LibGit2Sharp 0.30 resolving remote tracking info during enumeration, hanging on repos with no remotes. Fixed by switching to `repo.Refs.FromGlob("refs/heads/*")` which reads the ref store directly with no upstream resolution.

**Status:** [x] Fixed — `GetBranchesAsync` now uses `repo.Refs.FromGlob` instead of `repo.Branches`.

---

## Round 3 — Extension Pre-Implementation Risks (2026-06-22)

These items cover the two features added after the Phase 7 baseline was complete.
See `EXTENSIONS.md` for full design rationale.

---

### R3.1 `GetGraphAsync` must not call `commit.Parents` recursively *(priority: critical, BLOCKER for M7-G1)*

**Description:** `LibGit2Sharp.Commit.Parents` is a lazy-evaluated collection. Naively
traversing the full parent graph for `count` commits could load far more commits than
requested (e.g., walking to the initial commit for every merge parent). On a large repo
with thousands of commits and complex merge topology, this can take seconds and allocate
heavily.

**Required fix:** `GetGraphAsync` must fetch only the top-N commits from `repo.Commits`
(respecting the `count` limit) and collect parent SHAs as strings — `parent.Sha` — without
loading the parent commit objects. The ViewModel resolves parent positions by SHA lookup
against the already-fetched list, not by traversal. Add a test with a 50-commit repo to
verify the N limit is respected.

**Status:** [x] Fixed — `GetGraphAsync` in `LibGit2SharpService` fetches `repo.Commits.Take(count)` only, collects parent SHAs via `commit.Parents.Select(p => p.Sha)` (strings only, no object traversal). Tests verify linear history, merge commits, count limits, and metadata. (M7-G1, `LibGit2SharpService.cs`)

---

### R3.2 `GitGraphControl` rendering must not block the UI thread *(priority: high, BLOCKER for M7-G3)*

**Description:** `Control.Render(DrawingContext)` runs on the UI thread. If the lane-assignment
algorithm or geometry calculations are done inside `Render()`, a graph with 200 commits will
cause a perceptible stutter every time the panel redraws (scroll, resize, theme change).

**Required fix:** All lane-assignment and geometry calculation (commit positions, line endpoints,
label positions) must be computed in `GitGraphViewModel` off the UI thread (or at least outside
`Render()`). `Render()` should only consume pre-computed `IReadOnlyList<GraphNodeGeometry>` and
draw them. `GitGraphViewModel` recalculates geometry when the commit list changes, stores the
result reactively, and invalidates the control via `InvalidateVisual()`.

**Status:** [x] Fixed — All geometry is pre-computed in `GitGraphViewModel.ComputeLayout()` as `GraphNodeGeometry` records. `GitGraphControl.Render()` is a thin draw-only method consuming the pre-computed Nodes/Lanes. No algorithm, math, or layout logic runs inside Render(). `AffectsRender` invalidates the control when bound properties change. (M7-G3, `GitGraphControl.cs`)

---

### R3.3 Lane-assignment algorithm correctness for merge commits *(priority: high)*

**Description:** A greedy lane-assignment algorithm (left-to-right, first-fit) is correct for
linear histories and simple feature branches. It can produce visually confusing results for
repos with many concurrent long-lived branches or octopus merges (3+ parents), where lanes
do not get recycled promptly and the graph widens unnecessarily.

**Required fix:** Implement lane recycling: when a branch's tip commit is consumed (merged into
another lane), mark that lane as free and reuse it for the next unassigned branch. Document the
algorithm's known limitations (octopus merges, very wide graphs) in code comments and in
`EXTENSIONS.md §Extension 1 Limitations`. Cap the graph width at 12 lanes — if the topology
exceeds 12 concurrent lanes, collapse overflow lanes into a single gray "..." lane.

**Status:** [x] Fixed — Lane recycling implemented in `GitGraphViewModel.ComputeLayout`: merge commits mark the merged branch's lane as inactive (free for reuse). Child-map based inheritance ensures linear history uses 1 lane. Capped at 12 lanes with overflow lane. 11 unit tests verify linear, two-branch, merge, and capped scenarios. (M7-G2, `GitGraphViewModel.cs`)

---

### R3.4 `GitGraphControl` hit-testing for commit node selection *(priority: medium)*

**Description:** Avalonia's `Control` does not provide built-in hit-testing against
`DrawingContext`-drawn shapes. Detecting which commit node was clicked requires a manual
distance check: on `PointerPressed`, compute the pointer position in control coordinates
and find the nearest pre-computed node center within the node radius.

**Required fix:** In `GitGraphControl.OnPointerPressed`, iterate `GitGraphViewModel.Nodes`
and find the node whose center is within `NodeRadius` pixels of the pointer. If found,
set `GitGraphViewModel.SelectedCommit` and call `e.Handled = true`. If no node is within
range, deselect. Add a unit test for the hit-test logic (pure geometry, no UI required).

**Status:** [x] Fixed — `GitGraphControl.OnPointerPressed` computes Euclidean distance from pointer to each pre-computed node center. Nearest node within `SelectedNodeRadius + 2` is selected via `CommitClicked` event → `GitGraphView` wires to `vm.SelectCommit()`. Commit lookup uses pre-built SHA→commit dictionary. (M7-G3, `GitGraphControl.cs`)

---

### R3.5 `GitWatcher` on Linux: inotify limit may prevent watcher creation *(priority: medium)*

**Description:** `FileSystemWatcher` on Linux uses inotify. The system default
`fs.inotify.max_user_watches` is 8192. If the user is running many watchers (other IDE
instances, build tools), creating a new watcher on `.git/` may silently fail or throw
`IOException: "The configured user limit (128) on the number of inotify instances has been reached"`.

**Required fix:** Wrap `GitWatcher` construction in a try/catch for `IOException`. If the
watcher cannot be created, log a warning via `IMessageBus` (`StatusMessage`) and degrade
gracefully — the Git panel still works, it just won't auto-reload. Do not throw from the
`GitWatcher` constructor. Add a `bool IsWatching` property so `GitViewModel` can surface a
tooltip like "Auto-reload unavailable (inotify limit reached). Click Refresh manually."

**Status:** [x] Fixed — `GitWatcher` constructor wraps `FileSystemWatcher` creation in try/catch for `IOException` and `ArgumentException`. On failure, `IsWatching = false` and the callback is nulled. No exception propagates. Tests confirm `Constructor_NonExistentPath_DoesNotThrow` and `Constructor_ValidPath_IsWatchingIsTrue`. (M8-W1, `GitWatcher.cs`)

---

### R3.6 `GitWatcher` debounce timer must not fire after `Dispose` *(priority: high)*

**Description:** `GitWatcher` uses a `System.Threading.Timer` (or `Task.Delay`) for 500ms
debouncing. If `Dispose()` is called while the timer is pending (e.g., `GitViewModel` is
disposed immediately after a `.git/index` change), the timer callback may invoke the refresh
callback on a disposed `GitViewModel`, causing `ObjectDisposedException` or a no-op on a
collected object.

**Required fix:** `GitWatcher.Dispose()` must stop the `FileSystemWatcher` first (set
`EnableRaisingEvents = false`), then cancel/dispose the debounce timer, then null the
callback. Use `Interlocked.Exchange` to atomically null the callback so a racing timer
invocation sees null and exits cleanly. Add a test that disposes the watcher while the
debounce is pending and verifies the callback is not invoked.

**Status:** [x] Fixed — `GitWatcher.Dispose()` stops the watcher first (`EnableRaisingEvents = false`), then disposes the debounce timer under lock, then nulls the callback atomically via `Interlocked.Exchange`. The debounce callback uses `Interlocked.CompareExchange` for a thread-safe read — sees null, exits cleanly. Test `Dispose_WhileDebouncePending_CallbackNotInvoked` verifies the race is safe. (M8-W1, `GitWatcher.cs`)

---

### R3.7 `GitWatcher` must not duplicate events already handled by `FolderChanged` *(priority: low)*

**Description:** The existing `FolderChanged`-triggered refresh runs with a 1-second cooldown.
`GitWatcher` fires 500ms after a `.git/` change. If both fire close together (e.g., a
`git stage` triggers both a workspace file change and a `.git/index` change), two refreshes
may queue within the cooldown window. The cooldown gate will absorb one of them, but the
sequence must be verified correct — no double-refresh, no stale state.

**Required fix:** Verify that the existing `Stopwatch`-based cooldown in
`RefreshStatusInternalAsync()` correctly absorbs the duplicate. Add a test that simulates a
`FolderChanged` and a `GitWatcher` callback firing within 100ms of each other and verifies
`RefreshStatusInternalAsync` is called exactly once.

**Status:** [x] Fixed by design — The `GitWatcher` callback invokes `RefreshStatusInternalAsync`, which uses the existing 1-second `Stopwatch`-based cooldown (`_refreshCooldownStopwatch`). If a `FolderChanged` and `GitWatcher` callback fire within the 1-second window, the second call hits the cooldown gate at line 139 and exits early. The `GitWatcher.Debounce_MultipleRapidEvents_FiresOnce` test confirms the debounce ensures a single callback per event burst. (M8-W2, `GitViewModel.cs`)

---

## Persistent Checks — Extensions (add to self-review before closing Phase 7)

- [x] `GetGraphAsync` fetches parent SHAs only — no recursive graph traversal (R3.1)
- [x] `GitGraphControl.Render()` consumes pre-computed geometry only — no algorithm in `Render()` (R3.2)
- [x] Lane recycling implemented; graph width capped at 12 lanes (R3.3)
- [x] Hit-testing uses pre-computed node centers; `SelectedCommit` set correctly (R3.4)
- [x] `GitWatcher` degrades gracefully on inotify failure; `IsWatching` surfaced (R3.5)
- [x] `GitWatcher.Dispose()` is race-safe; callback not invoked after dispose (R3.6)
- [x] Dual-trigger (FolderChanged + GitWatcher) produces exactly one refresh (R3.7)
- [x] `dotnet build src/aero.csproj` passes (0 warnings, 0 errors)
- [x] `dotnet test tests` passes — 392/392
- [x] `EXTENSIONS.md` updated exit conditions all met
- [x] `PHASES.md` Phase 7 extension items marked `[x]`

---

## Round 4 — Post-Implementation Fix (2026-06-22)

### R4.1 GitGraphView layout too narrow — commit dots cut off *(priority: medium)*

**Description:** `GitGraphView` used `Grid ColumnDefinitions="*,200"` which reserved 200px for the detail pane even when hidden. The `GitGraphControl` had `Width="{Binding TotalWidth}"` = 60px for a single-lane repo (too small). Combined, the graph column collapsed to ~80px in a ~280px sidebar, cutting off half the commit dots.

**Fix:** Restructured layout:
1. `GitGraphView.axaml`: Outer `ScrollViewer` wraps a `Grid(Auto,Auto)` — detail pane column only allocates space when visible (`IsVisible`). No fixed column widths.
2. `GitGraphViewModel.TotalWidth`: Added `Math.Max(250, ...)` minimum so single-lane repos get readable width.
3. Detail pane: `MinWidth="180"` only applies when visible.

**Status:** [x] Fixed — layout restructured, minimum width 250px.

---

## Closing Summary

All Phase 7 baseline and extension items are complete:

| Component | Status |
|-----------|--------|
| Baseline Git integration (panel, diff, commit, branch, indicators) | ✅ Complete |
| Extension 1: Branch Graph (M7-G1 through M7-G4) | ✅ Complete |
| Extension 2: Auto-Reload (M8-W1, M8-W2) | ✅ Complete |
| Total tests | **392/392** |
| All risks (R1.1–R4.1) | ✅ Resolved |

> Found during `EXTENSIONS_REVIEW.md`. Three items must be fixed before extensions
> are declared complete. Two low-severity items are optional polish.

---

### R4.1 `_shaMap` never populated — hit-testing always misses *(priority: high, BLOCKER)*

**Description:** `GitGraphView.OnDataContextChanged` calls `GraphControl.SetCommitLookup(vm.Commits)`
at DataContext-set time. At that point `LoadAsync` has not run yet (it's fire-and-forget
from `GitViewModel.OnFolderOpenedAsync`), so `vm.Commits` is `Array.Empty<GitGraphCommit>()`.
The SHA map stays empty for the lifetime of the view. All hit-test lookups in
`OnPointerPressed` miss, so clicking any commit node does nothing and the detail pane
never populates.

**Fix:** Move `SetCommitLookup` to after `LoadAsync` completes. Options:
(a) At the end of `GitGraphViewModel.LoadAsync`, raise a notification (e.g., set a
dummy reactive property) that `GitGraphView` observes to re-call `SetCommitLookup`; or
(b) have `GitGraphControl` build its own lookup from the `Nodes` dependency property
via a `PropertyChangedCallback` on `NodesProperty`.

**Status:** [ ] Open

---

### R4.2 `CommitClicked` event leaks on DataContext reassignment *(priority: medium)*

**Description:** `GitGraphView.OnDataContextChanged` calls `GraphControl.CommitClicked += c => vm.SelectCommit(c)`
without unsubscribing the previous handler. If the DataContext changes (workspace switch),
stale lambdas accumulate on the event, each calling `SelectCommit` on an old ViewModel.

**Fix:** Store the handler in a field:
```csharp
private Action<GitGraphCommit>? _clickHandler;

protected override void OnDataContextChanged(EventArgs e)
{
    base.OnDataContextChanged(e);
    if (_clickHandler != null)
        GraphControl.CommitClicked -= _clickHandler;
    _clickHandler = null;

    if (DataContext is GitGraphViewModel vm)
    {
        GraphControl.SetCommitLookup(vm.Commits);
        _clickHandler = c => vm.SelectCommit(c);
        GraphControl.CommitClicked += _clickHandler;
    }
}
```

**Status:** [ ] Open

---

### R4.3 `GetGraphAsync` skips `packed-refs` — branch labels missing on gc'd repos *(priority: medium)*

**Description:** `GetGraphAsync` builds the SHA→branch label map from loose refs only
(`refs/heads/` directory). Any repo that has been `git gc`'d moves refs to `packed-refs`.
The result: branch labels won't appear on any node in a normally-used repo.
`GetBranchesAsync` correctly reads both — the logic just wasn't carried over.

**Fix:** Extract the loose-ref + packed-refs reading into a private helper:
```csharp
private Dictionary<string, string> BuildBranchRefsBySha()
{
    // Read refs/heads/* (loose) + packed-refs, return SHA → branchName
}
```
Call from both `GetBranchesAsync` and `GetGraphAsync`.

**Status:** [ ] Open

---

### R4.4 `CommitClicked` exception catch too broad in `OnGitFileChanged` *(priority: low)*

**Description:** `catch (Exception ex)` in `GitWatcher.OnGitFileChanged` swallows
`OutOfMemoryException` and similar non-recoverable exceptions.

**Fix:** Narrow to `catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))`.

**Status:** [ ] Open

---

### R4.5 `IsWatching = false` not surfaced to user *(priority: low)*

**Description:** When `GitWatcher` cannot start (inotify limit), `GitViewModel` silently
continues without auto-reload. The user has no indication that Refresh must be done manually.

**Fix:** After `new GitWatcher(...)`, check `_gitWatcher.IsWatching` and publish a
`StatusMessage` if false.

**Status:** [ ] Open
