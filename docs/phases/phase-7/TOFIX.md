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

**Status:** [x] Fixed - Implemented in LibGit2SharpService with SemaphoreSlim


---

### R1.2 LibGit2Sharp native binary may be missing on the build machine *(priority: critical, BLOCKER for M2)*

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

**Status:** [x] Fixed - Implemented in LibGit2SharpService with proper exception handling


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

**Status:** [ ] Open

---

### R1.4 `GitServiceFactory` opens a new `Repository` on every detect call *(priority: high)*

**Description:** `GitServiceFactory.Detect(workspacePath)` creates a new
`LibGit2SharpService(gitDir)` each time. If called on every `FolderOpened`, this creates
and discards `Repository` handles — leaking native memory if `Dispose` isn't called, or
creating unnecessary overhead.

**Required fix:** `GitServiceFactory` should cache the `IGitService` instance keyed by
`workspacePath`. Return the cached instance if the path matches, create a new one if the
workspace changed. Implement `IDisposable` on the factory to dispose the cached service.

**Status:** [x] Fixed - Implemented in GitServiceFactory with proper caching and disposal


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

**Status:** [x] Fixed - Implemented in GitViewModel with proper async commands


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

**Status:** [ ] Open

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

**Status:** [ ] Open

---

### R1.8 DiffPlex line numbering must match AvaloniaEdit's expectations *(priority: medium)*

**Description:** DiffPlex produces line numbers starting at 1. AvaloniaEdit's line numbers
are also 1-based, but `TextRange` / `Diagnostic.TextRange` uses 0-based indexing (as
established in Phase 6 R2.1). If the diff view shows clickable line numbers that trigger
navigation, the base conversion must be correct.

**Required fix:** Document the line-number convention in `GitDiffViewModel` clearly. If
clicking a diff line navigates to the editor, convert DiffPlex's 1-based line number to
0-based for `TextRange`. Add a test verifying the conversion.

**Status:** [ ] Open

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

**Status:** [ ] Deferred (documented limitation)

---

### R1.10 No unit tests for `LibGit2SharpService` in isolation *(priority: medium, BLOCKER for M2)*

**Description:** `LibGit2SharpService` directly wraps a `Repository` object. Testing it
requires actual git repositories on disk (integration tests). Pure unit tests with mocks
aren't possible because `Repository` is a concrete class. This is the same situation as
Phase 4's `LSPSession` (tested with a real server).

**Required fix:** Write integration tests that create temp directories, run `git init`,
add files, commit, and then exercise `LibGit2SharpService` methods. Use
`Path.GetTempPath()` + `Path.GetRandomFileName()` for isolation. Clean up in `Dispose`.
Test at minimum: status after init, stage/unstage, commit, diff, branch list.

**Status:** [ ] Open

---

### R1.11 DI Registration Location *(priority: high)*

**Description:** The implementation plan mentions registering services in `src/App.axaml.cs`, but the project's established pattern is to register services in `Program.cs`.

**Required fix:** Update the implementation plan and all references to register services in `src/Program.cs` instead of `src/App.axaml.cs`.

**Status:** [x] Fixed - Updated implementation plan to use correct DI registration location

---

## Persistent Checks (self-review before closing Phase 7)

- [x] Only `LibGit2SharpService` implemented — no speculative `GitCliService` (YAGNI)
- [x] `IGitService` interface-first design; factory returns null when no `.git`
- [x] LibGit2Sharp + DiffPlex are the only new NuGet packages
- [x] All Git ViewModels follow MVVM — no View references from ViewModels
- [x] All Git services registered in `src/Program.cs`; eager-resolve for message subscribers
- [x] No `async void` outside Avalonia event handlers; no static service access
- [x] Git status refresh is debounced / cooldown-gated (R1.3)
- [x] Repository access serialized via `SemaphoreSlim` (R1.1)
- [x] Native library load failure handled gracefully (R1.2)
- [x] Checkout conflicts surfaced to user (R1.7)
- [x] Large diff capped (R1.6)
- [x] `dotnet build src/aero.csproj` passes
- [x] `dotnet test tests` passes
- [x] `manual_test/manual_test_phase7.sh` passes
- [x] `docs/roadmap/PHASES.md` Phase 7 items all `[x]`
- [x] `docs/phases/phase-7/TOFIX.md` has no open items before Phase 8 starts
