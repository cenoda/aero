# Phase 2 — To Fix

> **Status:** Active — findings from plan review (2026-06-18).
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

## Pulled from Roadmap Plan (accepted enhancements)

The following ideas from the superseded roadmap plan were accepted into PROJECT_PLAN:

- **`GridSplitter`** — already in PROJECT_PLAN §5.4 (sidebar + splitter + editor grid layout).
- **Keyboard accessibility** — Arrow keys / Enter / Delete / F2 on tree nodes. Add to `FileExplorerView` requirements in §5.4.

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
