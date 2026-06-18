# ⛔ SUPERSEDED — Phase 2 Detailed Implementation Plan

> **Status:** This document and its children in `docs/superseded/phase2-roadmap-breakdown/` are superseded by the authoritative plan at [`docs/phases/phase-2/PROJECT_PLAN.md`](../phases/phase-2/PROJECT_PLAN.md).
>
> **Why two plans existed:** This roadmap plan was an early ambitious draft. A separate Phase 2 breakdown (`docs/phases/phase-2/`) was later created that matches the `PHASES.md` checklist and README scope. They contradicted each other on namespaces, model names, message contracts, lazy-vs-eager loading, and dependency policy.
>
> **Resolution (2026-06-18):** `docs/phases/phase-2/PROJECT_PLAN.md` is the single source of truth. This file is retained for reference only — its good ideas (IgnoreList, FileSystemChangeKind) have been pulled into PROJECT_PLAN.
>
> **DO NOT implement from this file.**

---

## What was kept from this plan

The following ideas from this plan were pulled into `docs/phases/phase-2/PROJECT_PLAN.md`:

- **`IIgnoreList` service** — prevents `node_modules` from freezing the UI. Added as an optional enhancement to PROJECT_PLAN.
- **`FileSystemChangeKind` enum** — cheap forward-compat for Phase 7 Git status overlays.
- **`GridSplitter`** — already in PROJECT_PLAN §5.4.

## What was discarded

- Lazy loading + virtualization (deferred; PROJECT_PLAN ships eager load)
- `IWorkspaceService` / workspace persistence (deferred to Phase 8 settings)
- `FileSystemNode` / `ProjectNode` / `FileTreeNodeViewModel` naming (PROJECT_PLAN uses `FileSystemEntry` / `ProjectInfo` / `FileExplorerNodeViewModel`)
- `OpenDocumentRequest` / `StatusBarMessageRequested` / `PromptUserInput` messages (PROJECT_PLAN uses `FolderChanged` / `PromptNewItem` / `PromptRename` / `ConfirmDelete`)
- NSubstitute + Microsoft.Reactive.Testing dependencies (PROJECT_PLAN ADR-7: no new NuGet packages)
- `async void OnDesktopExit` workspace save (PROJECT_PLAN doesn't persist workspace state)
