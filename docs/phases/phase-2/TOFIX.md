# Phase 2 — To Fix

> **Status:** Active — Rounds 1-7 all ✅ closed.
> Resolve all open items before declaring Phase 2 complete.

---

## Round 1 — Plan Review

### R1.1 Two competing Phase 2 plans *(priority: critical)*
`docs/superseded/phase2-roadmap-plan.md` + `docs/superseded/phase2-roadmap-breakdown/*` contradicted this PROJECT_PLAN on model names, message contracts, architecture, dependency policy, and scope.
**Fix:** Moved roadmap plan files to `docs/superseded/` (2026-06-18). `docs/phases/phase-2/PROJECT_PLAN.md` is the single source of truth. Updated `PHASES.md` link.
**Status:** ✅ RESOLVED

### R1.2 Optional `IIgnoreList` service *(priority: medium)*
The roadmap plan had a good idea: a pattern-based ignore list for `node_modules`, `bin`, `obj`, `.git`, etc. This prevents large folders from freezing the tree. PROJECT_PLAN ships eager loading — without an ignore list, opening any folder containing `node_modules` will freeze the UI enumerating it. Custom code (~60 lines), no NuGet, satisfies ADR-7.
**Fix:** Accepted. Folded into PROJECT_PLAN §2 (scope), §3 (architecture diagram), §5.2 (service spec), §6 (file layout), §7/M1 (milestone gate), `App.axaml.cs` (DI registration — singleton).
**Status:** ✅ ACCEPTED (2026-06-18)

### R1.3 `FileSystemChangeKind` enum for forward-compat *(priority: low)*
The roadmap plan defined `FileSystemChangeKind {Created, Deleted, Renamed, Changed}`. PROJECT_PLAN publishes a flat `FolderChanged(Path)` and reloads the whole affected subtree. Nothing consumes a change-kind today — adding it now is YAGNI. It's trivially cheap to add in Phase 7 when Git status actually needs it.
**Fix:** Deferred. Keep `FolderChanged(Path)` flat.
**Status:** ✅ DEFERRED to Phase 7

### R1.4 Renaming/deleting a file open in the editor *(priority: medium)*
If a file is open in an editor tab and the user renames or deletes it via the tree, the tab now points at a stale (deleted/renamed) path. PROJECT_PLAN §10 calls this a "known limitation". The proper fix needs a `FileRenamed`/`FileDeleted` message that `DocumentManager` subscribes to — that touches Phase 1 code you've deliberately frozen. Eliminating duplicate handling or potential data loss in this phase is a good plan.
**Fix:** Deferred to a later phase (likely Phase 7/8). Current safeguard: deleting a file from the tree does NOT close the tab — the buffer stays intact. Tab shows "[deleted]" indicator TBD.
**Status:** ✅ DEFERRED

### R1.5 Path normalization for duplicate detection *(priority: low, target: M3)*
`DocumentManager.OpenDocumentAsync` dedupes by exact `FilePath` string equality. Paths from the tree (different casing on case-insensitive FS, trailing separators, symlink targets) may not match paths from the file picker, causing duplicate tabs. The fix should NOT touch `DocumentManager` (Phase 1 freeze). Instead, normalize with `Path.GetFullPath()` at the `FileExplorerViewModel` call site before calling `OpenDocumentAsync`.
**Fix:** Added `Path.GetFullPath()` normalization in `FileExplorerViewModel.OpenFileAsync()` (called by `OpenSelectedFileAsync()`). Added tests with `..` relative paths: `OpenSelectedFileAsync_NormalizesDotDotPath`, `OpenFileAsync_NormalizesBeforeOpening`.
**Status:** ✅ RESOLVED (2026-06-18)

### R1.6 PHASES.md not reconciled with PROJECT_PLAN scope *(priority: critical)*
`PHASES.md` §2 still described lazy loading, `OpenDocumentRequest` message, full project parsing, and workspace persistence — none of which PROJECT_PLAN ships.
**Fix:** Reconciled `PHASES.md` §2 with PROJECT_PLAN decisions (2026-06-18): lazy load → deferred (eager + IgnoreList), `OpenDocumentRequest` → direct `DocumentManager` call, project parsing → extension recognition, persistence → deferred to Phase 8. Model names aligned to `FileSystemEntry`/`ProjectInfo`.
**Status:** ✅ RESOLVED

---

## Round 2 — M1 Code Review (2026-06-18)

Findings from review of the M1 source (`IgnoreList`, `FileSystemService`,
`ProjectLoader`) and their tests. All medium-priority items were fixed
before commit; minor cleanups included.

### R2.1 IgnoreList didn't filter watcher events for files inside ignored dirs *(priority: medium, BLOCKER for M5)*
Original `IsIgnored` only checked the leaf name. For the tree (M1) this was
harmless because `bin/` is filtered out and we never recurse into it. But the
M5 watcher fires individual events for every changed file — a build that
writes `/repo/bin/Debug/app.dll` would emit an event with leaf `app.dll`,
which the original code would NOT ignore. Build-output churn would flood the
tree refresh path.
**Fix:** Rewrote `IgnoreList` to split paths into segments once at check
time. For files, ANY ancestor directory segment matching a directory
pattern is enough to ignore. For directories, only the leaf is checked.
Wildcard patterns (`*.ext`) are now correctly classified as file-only —
directories named `x.tmp` are no longer hidden.
**Tests added:** `File_InsideIgnoredDirectory_IsIgnored`, `File_DeepInsideIgnoredDirectory_IsIgnored`, `File_NamedLikeIgnoredDirectory_ButNotInside_IsNotIgnored`, `File_BackslashSeparators_AreTreatedAsPathSeparators`, `Directory_WithTrailingSeparator_StillMatches`, `WildcardPattern_DoesNotMatchDirectory`, `DirectoryPattern_FileWithSameLeafName_IsNotIgnoredByLeafAlone`.
**Status:** ✅ RESOLVED

### R2.2 `CreateFileAsync` silently truncated existing files *(priority: medium, data-loss risk)*
`File.Create(target)` overwrites. When M4 wires this to the "New File" context
action, a name collision would silently destroy a file on disk — the worst
possible UX. The plan calls for collision validation in the VM, but the
service should defend against the silent-data-loss case regardless.
**Fix:** Switched to `FileMode.CreateNew` via `FileStream`. Throws
`IOException` if the file exists; caller (M4 VM) is responsible for
pre-validation and a friendly error message.
**Test added:** `CreateFileAsync_ExistingFile_DoesNotOverwrite` — writes
content, calls `CreateFileAsync` with same name, asserts `IOException` AND
that the original content is intact.
**Status:** ✅ RESOLVED

### R2.3 Misleading `Task.Yield()` comment in `GetDirectoryEntriesAsync` *(priority: low)*
The original comment claimed enumeration "yields via MoveNext", but
`MoveNext()` is synchronous and never returns to the message pump. The
method's actual responsiveness depends on the VM calling it via `Task.Run`
(PROJECT_PLAN §5.3). The `Task.Yield()` fired *after* all the work and did
nothing for responsiveness.
**Fix:** Replaced `await Task.Yield()` with `await Task.CompletedTask` and
updated the comment to be honest: enumeration is sync; the VM owns the
off-thread dispatch. M2 must remember this.
**Status:** ✅ RESOLVED

### R2.4 `IIgnoreList.IsIgnored`'s `isDirectory` flag was dead surface *(priority: low)*
After R2.1, the flag now actually means something (see fix above). The
interface doc was updated to match the new contract.
**Status:** ✅ RESOLVED (folded into R2.1)

### R2.5 Cancellation tokens not honored in Create/Rename/Delete/Exists *(priority: low)*
Interface says "honour `CancellationToken` on every I/O-bound method" but
only `GetDirectoryEntriesAsync` checked it. `DeleteAsync(recursive)` on a
large tree could be slow.
**Fix:** Added an up-front `ct.ThrowIfCancellationRequested()` to each
method. Tests added.
**Status:** ✅ RESOLVED

### R2.6 `ProjectLoader.DetectProjects` was one-level but plan said "and subdirectories" *(priority: low)*
One-level is the right call (avoids deep traversal). Plan wording was wrong.
**Fix:** Updated PROJECT_PLAN §5.2 to say "workspace root only (one level
deep)" and noted the rollback from the original plan.
**Status:** ✅ RESOLVED

### R2.7 Unused `using System.Linq;` *(priority: trivial)*
Found in `IgnoreList.cs` (since rewritten without it) and `ProjectLoaderTests.cs`.
**Fix:** Removed.
**Status:** ✅ RESOLVED

---

## Pulled from Roadmap Plan (accepted enhancements)

The following ideas from the superseded roadmap plan were accepted into PROJECT_PLAN:

- **`GridSplitter`** — already in PROJECT_PLAN §5.4 (sidebar + splitter + editor grid layout).
- **Keyboard accessibility** — Arrow keys / Enter / Delete / F2 on tree nodes. Add to `FileExplorerView` requirements in §5.4.

**Status:** ✅ RESOLVED

---

## Round 3 — M2 Review (2026-06-18)

Findings from M2 implementation: tree VMs, sidebar layout in
`MainWindow.axaml`, DI wiring, MessageBus subscription. The M2 gate
(app launches; sidebar visible and empty; no Phase 1 regression)
passes per `manual_test_phase1.sh` (all 9 sub-tests green, no Phase 1
regressions, screenshots in `manual_test_screenshots/`).

### R3.1 `Material.Icons.Avalonia` does not work on Avalonia 11.3 *(priority: medium, BLOCKER for visual polish)*
`docs/LIBRARIES.md` lists this library as "1.*" for use in Phase 2/8.
Investigation during M2 found the package is in a broken state across
all available versions:

| Version | Status on Avalonia 11.3 |
|---------|------------------------|
| `1.2.2` (latest 1.x, currently in `aero.csproj`) | Embedded XAML lacks `x:Class`. Including `MaterialIcon.xaml` throws `XamlLoadException` at app startup. |
| `2.4.3` (latest 2.x in cache) | Requires `Avalonia >= 12.0.0` — restore fails with `NU1605` package downgrade. |
| `3.0.2` (latest 3.x in cache) | Same — requires Avalonia 12. |

The project is pinned to `Avalonia 11.3.*`, so neither 2.x nor 3.x is
available without an Avalonia major-version bump.

**Current M2 mitigation:** `FileExplorerView` uses simple text glyphs
(`▸` for dirs, `•` for files, `◆`/`#`/`⬡` for project files) via a new
`Glyph` property on `FileExplorerNodeViewModel`. The view binds to
`Glyph`; the VM still records `IconKind` (a `Material.Icons.Avalonia`
kind name string) for forward compatibility. When the icon decision
lands, swap the XAML binding from `{Binding Glyph}` to
`{Binding IconKind}` and re-add the `<StyleInclude>` in `App.axaml`.

**Decision options for the user:**

1. **Bump Avalonia to 12.0.0** + adopt `Material.Icons.Avalonia 3.0.2`.
   Risks: Avalonia 12 is a major version — AvaloniaEdit,
   and the existing `Fody`/`ReactiveUI.Fody` setup may need updates.
   Note: Dock.Avalonia was abandoned 2026-06-25 (see Phase 8.1).
2. **Stay on Avalonia 11.3** + adopt a different icon library (or
   none). Candidates: hand-rolled SVG, `Projektanker.Icons.Avalonia`
   (FontAwesome / Material subset, may have a 1.x version compatible
   with Avalonia 11).
3. **Stay on Avalonia 11.3** + keep the text-glyph approach through
   Phase 2 (M3–M5 don't need richer visuals), revisit in Phase 8 UI
   Polish when the rest of the layout work is done.

**Status:** ✅ RESOLVED IN PHASE 8.5 (originally deferred 2026-06-18, resolved 2026-06-22).
User chose option 3 (keep text glyphs through Phase 2, revisit in
Phase 8 "UI Polish"). Phase 8.5 resolved this with **Phosphor Icons** —
8 embedded `StreamGeometry` vectors (MIT licensed), no NuGet dependency.
`FileExplorerNodeViewModel.Glyph` maps legacy `IconKind` strings to new
icon resource keys; `GlyphGeometry` resolves via `TryFindResource`. Both
the file tree and editor tabs now render `PathIcon` glyphs. See
`docs/phases/phase-8/8.5-icon-decision/` for the full decision and
implementation.

---

## Round 4 — M2 Review Follow-up (2026-06-18)

Findings from a review-agent pass on M2. Two real items landed:
one latent M3 blocker (R3.2), one cleanup. The M2 implementation was
otherwise solid — cancellation discipline, off-thread dispatch,
FolderOpened integration all correct.

### R3.2 Eager recursive load becomes a hazard the moment M3 wires Open Folder *(priority: medium, would-have-blocked-M3)*
`BuildNodeAsync` recursed into every non-ignored directory with no
depth or node-count cap. M2 shipped this without consequence because
no user-reachable folder could be opened yet (the picker is M3). The
moment M3 wires Open Folder, a user pointing at their home directory
or `/` would eagerly walk and materialize a VM + ObservableCollection
for *every* file and folder. `IIgnoreList` only excludes build/VCS
dirs (`bin`, `obj`, `node_modules`, `.git`) — it does nothing for
large data/media/source trees.

**Fix:** Switched from eager recursive walk to lazy-load-on-expand
(M2.5 slice).

Implementation:
- `FileExplorerViewModel.BuildTreeAsync` removed; replaced with
  `BuildRootLevelAsync` that enumerates ONLY the root folder.
- New `EnsureChildrenLoadedAsync(node)` populates a single directory's
  children on demand. Idempotent (`AreChildrenLoaded == true` → no-op).
- `FileExplorerNodeViewModel.AreChildrenLoaded` flips to `false` on
  construction for directories; view code-behind subscribes to
  `TreeViewItem.Expanded` and calls `EnsureChildrenLoadedAsync` on
  each new container.
- Directories get a `PlaceholderChild` sentinel pre-added so the
  TreeView renders the expander arrow before children are loaded.
  The placeholder is removed during the actual load.
- Per-node cancellation in a `ConcurrentDictionary` so rapid
  expand/collapse cancels in-flight expansions cleanly.
- `LoadFolderAsync` now cancels ALL in-flight child loads (not just
  the root) so a stale child load can't write into a node that's
  about to be discarded by the new tree.

**Tests added (9 new, total 192/192):**
- `LoadFolderAsync_DirectoriesStartUnloaded`
- `LoadFolderAsync_DirectoriesHaveOnlyPlaceholderChild`
- `LoadFolderAsync_FilesHaveNoChildren`
- `LoadFolderAsync_DoesNotEnumerateSubdirectories` (R3.2 regression test)
- `EnsureChildrenLoadedAsync_PopulatesChildrenAndClearsPlaceholder`
- `EnsureChildrenLoadedAsync_OnFile_IsNoOp`
- `EnsureChildrenLoadedAsync_DoubleCall_OnlyLoadsOnce`
- `EnsureChildrenLoadedAsync_RapidExpandCollapses_CancelsPrevious`
- `EnsureChildrenLoadedAsync_LoadFolderCancelsInflightChildLoads`
- `EnsureChildrenLoadedAsync_NullNode_Throws`

**Status:** ✅ RESOLVED

### R3.3 Unused `using System.Linq;` and redundant `DisplayName` pass-through *(priority: trivial, cleanup)*
Reviewer noted the same hygiene issue as Phase 1 R4.4 (unused LINQ
import) plus a redundant `DisplayName => Name` accessor that was
unused by the view (the view bound to `Name` directly anyway).
**Fix:** Removed `using System.Linq;`. Removed `DisplayName` from the
node VM and switched the view's `TextBlock` binding from
`{Binding DisplayName}` to `{Binding Name}`.
**Status:** ✅ RESOLVED (folded into M2.5)

---

## Round 5 — M3 Review (2026-06-18)

Findings from implementing M3 (Open Folder & File Activation). All items
resolved; no open blockers.

### R5.1 `File → Open Folder` picker and `FolderOpened` message flow *(priority: critical)*
Added `OpenFolderCommand` to `ShellViewModel` using Avalonia's
`IStorageProvider.OpenFolderPickerAsync`. On selection it publishes the
existing `FolderOpened(Path)` message; `FileExplorerViewModel` subscribes
and loads the tree. Verified end-to-end via the new
`manual_test_phase2_m3.sh` smoke test (tree loads, directory expands,
file opens in a tab).

Also added an optional CLI startup folder: passing a directory path as the
first argument opens that folder immediately. This is additive and made
headless verification of the load/expand/activate path reliable under
Xvfb.
**Status:** ✅ RESOLVED

### R5.2 `{Binding !RootPath}` did not update the empty-state hint *(priority: medium)*
After loading a folder, `FileExplorerViewModel.StatusText` updated to
"N entries" but the "No folder open" hint remained visible and the
`TreeView` rendered the loaded items. The `IsVisible` binding
`{Binding !RootPath}` on the hint did not react to the `string? RootPath`
property change even though `[Reactive]` was weaving notifications.
Switching to a dedicated boolean `HasRootPath` property (set in lockstep
with `RootPath`) and binding `{Binding !HasRootPath}` resolved it,
matching the existing `EditorViewModel.HasDocument` pattern.
**Status:** ✅ RESOLVED (2026-06-18)

### R5.3 Per-container double-click wiring missed lazy-loaded child items *(priority: medium)*
Initial M3 attached `DoubleTapped` inside `ContainerPrepared`. Root items
received the handler, but child `TreeViewItem`s created during lazy
expansion did not reliably open on double-click. Moved the handler to the
`TreeView` itself and walked the visual tree from `e.Source` to the
containing `TreeViewItem`. This covers root and nested items uniformly.
**Status:** ✅ RESOLVED (2026-06-18)

### R5.4 Enter-on-directory was a silent no-op *(priority: medium)*
`OnTreeKeyDown` toggled `SelectedNode.IsExpanded` for directories, but no
XAML bound the container `TreeViewItem.IsExpanded` to the node VM's
`IsExpanded` property. The VM property changed, but the visual container
never expanded/collapsed, so no `Expanded` event fired and lazy-load did
not trigger.
**Fix:** Added a `TreeView.Styles` setter binding `TreeViewItem.IsExpanded`
two-way to the node VM's `IsExpanded`. Updated `manual_test_phase2_m3.sh`
to expand `src` via Enter, so the path is exercised in the smoke test.
**Status:** ✅ RESOLVED (2026-06-18)

---

## Round 6 — M4 Context Menu Operations (2026-06-19)

Findings from M4 implementation: New File, New Folder, Rename, Delete
commands on the File Explorer tree context menu, plus keyboard shortcuts
(F2=rename, Del=delete). All items resolved; no open blockers.

### R6.1 Dialog bridge pattern for VM→View communication *(priority: critical)*
Four new MessageBus record types (`PromptNewItem`, `PromptRename`,
`ConfirmDelete`) carry `Action<T?>` callbacks that the subscriber
(MainWindow code-behind) invokes with the user's dialog response.
`ConfirmDelete` maps `null→false` (via `??`) so the `OnResult` contract
is always `bool` on the ViewModel side.

**Status:** ✅ RESOLVED

### R6.2 Context-menu command binding — alternative (b) *(priority: medium)*
Used the `Owner` back-reference pattern: each `FileExplorerNodeViewModel`
has an `Owner` property pointing at the owning `FileExplorerViewModel`.
XAML binds `{Binding Owner.NewFileCommand}` with `CommandParameter="{Binding}"`.
This avoids polluting the node VM with commands while keeping the
ContextMenu declarative.

**Status:** ✅ RESOLVED

### R6.3 New File/Folder target logic — file vs directory selection *(priority: medium)*
When a directory node is selected, the new item is created inside it.
When a file node is selected, the new item is created in its parent
directory. When there is no selection (null command parameter), the
handler returns early (no-op).

**Status:** ✅ RESOLVED

### R6.4 Rename/Delete safety with open documents *(priority: medium)*
Renaming or deleting a file that's open in the editor does NOT close or
corrupt the tab. Regression tests (`RenameCommand_DoesNotAffectDocumentManager`,
`DeleteCommand_DoesNotAffectDocumentManager`) assert `DocumentManager.Documents`
is unchanged after the operation. The proper fix (closing/stale-indicator) is
deferred — see R1.4.

**Status:** ✅ RESOLVED (regression tests added)

---

## Round 7 — Tree Refresh After Create/Rename/Delete (2026-06-19)

Findings from code review of M4 implementation. Two real functional bugs
and one test-coverage gap. All items resolved; no open blockers.

### R7.1 `RefreshParentNodeAsync` is a silent no-op on loaded parents *(priority: critical)*

**Description:** `RefreshParentNodeAsync(node)` calls
`EnsureChildrenLoadedAsync(parent)`, which early-returns at
`if (node.AreChildrenLoaded) return;` because any visible node's parent
already has `AreChildrenLoaded == true`. The tree never reflects the
change.

**Root cause:** `EnsureChildrenLoadedAsync` is designed for initial lazy
load (on first expand). It intentionally no-ops on loaded nodes. Using it
for "reload after mutation" was the wrong abstraction.

Additionally, create operations targeted the wrong level:
`OnNewFileAsync`/`OnNewFolderAsync` called
`RefreshParentNodeAsync(node?.IsDirectory == true ? node : node?.Parent)`,
but the parent of the *parent* (grandparent) was refreshed instead of the
directory itself — when creating inside D, D.Parent gets refreshed, not D.

**Fix:**
1. Added `ForceReloadChildrenAsync(FileExplorerNodeViewModel dir)` which
   resets `dir.AreChildrenLoaded = false`, then calls
   `EnsureChildrenLoadedAsync(dir)`.
2. Create: target = `node?.IsDirectory == true ? node : node?.Parent`.
   Null → `LoadFolderAsync(RootPath)`. `IsExpanded` set to `true` so the
   new item is visible.
3. Rename/Delete: target = `node?.Parent`. Null → `LoadFolderAsync(RootPath)`.
4. Removed the buggy `RefreshParentNodeAsync` helper.

**Status:** ✅ RESOLVED

### R7.2 Nested tree-state test coverage gap *(priority: high)*

**Description:** All command tests assert disk state
(`_fs.ExistsAsync(...)`) and `DocumentManager` state, but never assert
the tree's `Children` collection — and all operate at root level where
`Parent == null` falls through to `LoadFolderAsync(RootPath)` full-reload
fallback.

**Fix:** Added 3 tests that build a 2-level tree, expand both levels,
execute the command, then assert `Children` contains/does-not-contain the
expected node:
- `CreateFile_Nested_UpdatesParentChildren` — create file inside expanded
  subdirectory, assert `Children` has the new node.
- `DeleteFile_Nested_RemovesFromParentChildren` — delete file inside
  expanded subdirectory, assert `Children` no longer has deleted node.
- `RenameFile_Nested_UpdatesParentChildren` — rename file inside
  expanded subdirectory, assert old name gone and new name present.

**Status:** ✅ RESOLVED

---

## Round 8 — M5 Review (FileSystemWatcher & Auto-Refresh)

Findings from implementing M5 (debounced filesystem watcher, manual refresh,
DI wiring, and integration tests). Two real issues: one pre-existing DI bug
that M5 exposed, and one known UX limitation to revisit in Phase 8.

### R8.1 `IgnoreList` singleton created with empty patterns in the real app *(priority: critical, BLOCKER found during M5 manual test)*

**Description:** The manual smoke test for M5 showed `bin/` and `obj/`
directories appearing in the file-explorer tree after build-output churn —
contradicting the `IIgnoreList` contract and the passing unit tests.

**Root cause:** `IgnoreList` has two public constructors:
1. parameterless `IgnoreList()` → loads `DefaultPatterns`
2. `IgnoreList(IEnumerable<string> initialPatterns)` → used by tests

`App.axaml.cs` registered it as `services.AddSingleton<IIgnoreList, IgnoreList>();`.
Microsoft.Extensions.DependencyInjection selected the constructor with the
most resolvable parameters — the `IEnumerable<string>` constructor — and
passed an empty enumerable because no `IEnumerable<string>` services were
registered. The real app therefore ran with an empty ignore list, while all
unit tests that called `new IgnoreList()` directly saw the defaults.

**Fix:** Changed DI registration to an explicit factory that uses the
parameterless constructor:
```csharp
services.AddSingleton<IIgnoreList>(_ => new IgnoreList());
```
Also added a regression test `GetDirectoryEntriesAsync_FiltersBinObj_AfterBuildChurn`
to cover the exact manual-test scenario at the service level.

**Status:** ✅ RESOLVED (2026-06-19)

### R8.2 Auto-refresh reloads the whole root and collapses expansion state *(priority: medium, deferred)*

**Description:** `FolderChanged` is intentionally flat (per TOFIX R1.3) and
`FileExplorerViewModel` responds by reloading the entire root folder. This
replaces `RootNodes` from scratch, so any directories the user had expanded
are collapsed again. This is annoying during active external edits.

**Fix:** Deferred to Phase 8 / a future Phase 2.x polish slice. The proper
fix is per-node refresh: keep a `FileChanged`/`FileCreated`/`FileDeleted`
message kind (deferred per R1.3) and surgically update the affected node's
parent children instead of rebuilding the whole tree. For Phase 2 the
current behavior is correct per spec and keeps the implementation simple;
the limitation is documented here and in the manual test checklist.

**Status:** ✅ DEFERRED

### R8.3 `StatusMessage` handler updated UI from a background thread *(priority: medium, found during review)*

**Description:** `FileSystemWatcherService.OnError` runs on a `FileSystemWatcher`
thread-pool thread. It publishes `StatusMessage`, and `MessageBus.Publish` invokes
handlers synchronously on the publisher's thread. `ShellViewModel`'s handler was
setting the `[Reactive]` `StatusText` property directly, which would have thrown
on Avalonia's binding system and been swallowed by the bus's try/catch — so the
"watcher failed, manual refresh still works" warning would never reach the user.

**Fix:** Marshalled the `StatusText` update in `ShellViewModel` onto
`Dispatcher.UIThread` using `Dispatcher.Post`, mirroring the thread discipline in
`FileExplorerViewModel.OnFolderChangedAsync`.

**Status:** ✅ RESOLVED (2026-06-19)

### R8.4 `FileExplorerViewModel` silently swallowed `IFileSystemWatcherService.Watch` failures *(priority: low, found during review)*

**Description:** The `FolderOpened` handler called `_watcher.Watch(msg.Path)`
outside any try/catch. If watching failed (permissions, deleted folder, inotify
limits on Linux), `MessageBus` swallowed the exception and auto-refresh died
silently.

**Fix:** Wrapped `_watcher.Watch(...)` in a try/catch that publishes a
`StatusMessage` explaining the failure and reminding the user that manual refresh
still works.

**Status:** ✅ RESOLVED (2026-06-19)

### R8.5 `FileExplorerViewModel.Dispose` disposed a DI singleton it doesn't own *(priority: low, found during review)*

**Description:** `FileExplorerViewModel` called `_watcher.Dispose()` in its own
`Dispose()`. The watcher is registered as a DI singleton and is disposed by the
container on app exit. Calling `Dispose` here happened to be safe today because
`Dispose` is idempotent and there is only one consumer, but it violates the
ownership boundary.

**Fix:** Changed the VM's `Dispose()` to call `_watcher.StopWatching()` only,
leaving final disposal to the DI container.

**Status:** ✅ RESOLVED (2026-06-19)

---

## Persistent Checks

Use these as a self-review checklist before closing Phase 2:

- [x] No new NuGet packages were added without updating `docs/LIBRARIES.md`.
- [x] All new services are registered in `src/App.axaml.cs` and documented in `docs/architecture/CORE_INFRASTRUCTURE.md`.
- [x] All new public service methods are covered by unit tests.
- [x] `FileSystemWatcherService` disposes its watcher and timer on stop/exit.
- [x] `FileExplorerViewModel` unsubscribes from MessageBus in `Dispose()`.
- [x] No `async void` outside Avalonia event handlers.
- [x] No `!` null-forgiving operator without a comment explaining safety.
- [x] Phase 1 regression tests still pass (`dotnet test tests` — 227/227 as of M5).
- [x] Manual smoke test `manual_test_phase2.sh` passes.
- [x] `docs/phases/phase-2/TOFIX.md` has no open items.
