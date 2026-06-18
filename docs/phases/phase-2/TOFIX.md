# Phase 2 — To Fix

> **Status:** Active — Round 1 ✅ closed, Round 2 ✅ closed, Round 3 (M2) ⏳ open.
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
**Fix:** Add `Path.GetFullPath()` normalization in `FileExplorerViewModel` when opening files. Add a test with a path containing `..` or trailing separator.
**Status:** 📋 TODO — implement in M3

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
   Risks: Avalonia 12 is a major version — AvaloniaEdit, Dock.Avalonia,
   and the existing `Fody`/`ReactiveUI.Fody` setup may need updates.
2. **Stay on Avalonia 11.3** + adopt a different icon library (or
   none). Candidates: hand-rolled SVG, `Projektanker.Icons.Avalonia`
   (FontAwesome / Material subset, may have a 1.x version compatible
   with Avalonia 11).
3. **Stay on Avalonia 11.3** + keep the text-glyph approach through
   Phase 2 (M3–M5 don't need richer visuals), revisit in Phase 8 UI
   Polish when the rest of the layout work is done.

**Status:** ⏸ PAUSED — awaiting user decision. Until decided, M2 ships
with the text-glyph approach (which is fully functional, just visually
plain). See screenshot `manual_test_screenshots/aero_test_01_initial.png`.

---

## Persistent Checks

Use these as a self-review checklist before closing Phase 2:

- [ ] No new NuGet packages were added without updating `docs/LIBRARIES.md`.
- [ ] All new services are registered in `src/App.axaml.cs` and documented in `docs/architecture/CORE_INFRASTRUCTURE.md`.
- [ ] All new public service methods are covered by unit tests.
- [ ] `FileSystemWatcherService` disposes its watcher and timer on stop/exit.
- [ ] `FileExplorerViewModel` unsubscribes from MessageBus in `Dispose()`.
- [ ] No `async void` outside Avalonia event handlers.
- [ ] No `!` null-forgiving operator without a comment explaining safety.
- [ ] Phase 1 regression tests still pass (`dotnet test tests`).
- [ ] Manual smoke test `manual_test_phase2.sh` passes.
- [ ] `docs/phases/phase-2/TOFIX.md` has no open items.
