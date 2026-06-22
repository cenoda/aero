# Phase 7 — Extensions

> **Status:** Pre-implementation
> **Date:** 2026-06-22
> **Extends:** Phase 7 baseline (Git Integration), which is complete (362/362 tests, all exit conditions met)

This document records the two features added to Phase 7 **after** the baseline was declared
complete. Phase 7 was intentionally not closed so these could be absorbed before moving on.
If a similar situation arises in a future phase, this file is the precedent.

---

## Why Here, Not a New Phase

Phase 7's baseline was complete and reviewed but **not yet committed to closed status** when
these features were requested. Because both features:

- Are Git-domain work (same `IGitService` abstraction, same `GitPanelView` surface)
- Do not alter Phase 7's baseline contracts (no interface changes)
- Are additive (new tab, new watcher) with no regression risk to existing code

...they belong in Phase 7 as extensions rather than in a `phase-7.5` folder. A sub-phase is
warranted only when the baseline phase is already fully closed and the new work starts from a
clean break. That is not the case here.

The record of what was in the original baseline vs. what was added is preserved in this file.

---

## Baseline Scope (Already Complete)

The following was delivered in the Phase 7 baseline:

- `IGitService` interface + `GitModels` records
- `LibGit2SharpService` — thread-safe, native-library-safe, diff-capped
- `GitServiceFactory` — caches per workspace path
- `GitViewModel` + `GitFileStatusViewModel` — staged/unstaged list, stage/unstage/commit commands
- `GitDiffViewModel` + `GitDiffView` — unified diff with gutter and line numbers
- `GitPanelView` — dual-column staged/unstaged, commit box, branch checkout
- Status bar branch indicator
- Editor tab and file tree git status glyphs
- 25 git-related unit/integration tests (362 total)

---

## Extension 1 — Branch Graph

### What

A visual DAG (directed acyclic graph) of the commit history, rendered as a custom
Avalonia `Control`. Each commit is a node (circle). Each branch is a colored lane.
Merge commits connect lanes with diagonal lines. The active HEAD commit is highlighted.

This is the "Git Graph" view familiar from VS Code's Git Graph extension and GitKraken.

### Why

The commit log (`GetLogAsync`) already exists but is unused in the UI. A plain list of
commit messages is hard to reason about in a branchy repo. The graph makes the branch
topology immediately visible.

### Scope (Phase 7 Extension)

**In scope:**
- New `GitGraphViewModel` and `GitGraphView` (custom `Control` with `DrawingContext`)
- New `GetGraphAsync(int count)` on `IGitService` — returns commits with parent SHA list
- Lane-assignment algorithm (greedy left-to-right: each branch head gets its own lane;
  merges draw a connecting line from child lane to parent lane)
- Click a commit node → populate `GitGraphCommitDetailViewModel` in a side pane
  (SHA, author, date, message, changed file count)
- "Branch Graph" tab inside `GitPanelView` (tab switcher: Changes | Graph)
- Branch/tag labels rendered next to their commit node
- Scroll vertically through up to 200 commits (configurable constant)

**Out of scope (document as limitation):**
- Interactive rebase (drag to reorder)
- Cherry-pick from graph
- Reset to commit from graph
- Zoom / minimap of graph
- Remote branch visualization (no fetch/pull in Phase 7)
- Full Sugiyama layout algorithm (greedy lane assignment is sufficient and correct)

### Key Design Decisions

**D-G1: Custom `Control`, not `Canvas`**
`Canvas` requires manual position management from C# code-behind, which mixes layout
logic with view. A custom `Control` that overrides `Render(DrawingContext)` keeps all
drawing logic in one place and works with Avalonia's invalidation model correctly.

**D-G2: Lane assignment algorithm**
Simple greedy algorithm:
1. Walk commits from newest to oldest (post-order from log).
2. Each branch HEAD gets its own lane index (assigned incrementally).
3. When a commit has two parents (merge), the second parent's lane gets recycled after
   the merge point.
4. Lanes are colored by hashing the branch name to a palette of 8 distinct colors.

This produces a correct graph for all non-degenerate cases. Complex octopus merges
(3+ parents) are rendered by drawing lines from the commit node to all parent nodes.

**D-G3: `GetGraphAsync` returns parent SHAs, not full parent commits**
Returning `IReadOnlyList<string> ParentShas` per commit avoids pulling the entire
commit object tree for deep histories. The ViewModel resolves parent positions from
the already-fetched commit list by SHA lookup.

**D-G4: Tap to select, not double-click**
Single-click on a commit node selects it and shows the detail panel. Double-click is
reserved for a future "checkout this commit" action (detached HEAD) — not in scope now.

### New Files

```
src/
  Services/Git/
    GitGraphModels.cs             ← GitGraphCommit record (Sha, Message, Author, Date, ParentShas, BranchLabels)
  ViewModels/
    GitGraphViewModel.cs          ← Owns commit list, lane layout, selected commit
    GitGraphCommitDetailViewModel.cs ← Detail panel for selected commit
  Views/
    GitGraphView.axaml            ← Tab switcher wrapper
    GitGraphView.axaml.cs
    GitGraphControl.cs            ← Custom Control (DrawingContext rendering)
tests/
  ViewModels/
    GitGraphViewModelTests.cs     ← Lane assignment, commit detail, null-service handling
```

### Modified Files

```
src/Services/Git/IGitService.cs           ← Add GetGraphAsync(int count, CancellationToken ct)
src/Services/Git/LibGit2SharpService.cs   ← Implement GetGraphAsync
src/ViewModels/GitViewModel.cs            ← Add GitGraphViewModel property; wire tab selection
src/Views/GitPanelView.axaml              ← Add tab control (Changes | Graph)
src/App.axaml.cs                          ← Register GitGraphViewModel
```

### IGitService Addition

```csharp
/// <summary>
/// Gets commit graph data (commits with parent SHAs) for rendering a branch graph.
/// </summary>
Task<IReadOnlyList<GitGraphCommit>> GetGraphAsync(int count, CancellationToken ct);
```

### Milestones

| Milestone | Deliverable |
|-----------|-------------|
| M7-G1 | `GitGraphModels.cs`, `GetGraphAsync` on interface + `LibGit2SharpService` |
| M7-G2 | `GitGraphViewModel` with lane-assignment algorithm, unit tests |
| M7-G3 | `GitGraphControl` custom rendering, `GitGraphView`, tab switcher in `GitPanelView` |
| M7-G4 | `GitGraphCommitDetailViewModel` + detail pane in XAML |

---

## Extension 2 — Auto-Reload of Git State

### What

A `FileSystemWatcher` targeted at the `.git/` directory. When `HEAD`, `index`, or
`COMMIT_EDITMSG` change (i.e., a commit, checkout, stage, or reset happened outside the
IDE), the Git panel refreshes automatically without user interaction.

### Why

The current refresh is triggered only by `FolderChanged` (workspace file saves) or the
manual Refresh button. If the user runs `git commit`, `git checkout`, or `git pull` in
an external terminal, the panel shows stale state until they click Refresh manually.
Auto-reload eliminates this friction.

### Scope (Phase 7 Extension)

**In scope:**
- `GitWatcher` — a focused `FileSystemWatcher` on `.git/` watching `HEAD`, `index`,
  `COMMIT_EDITMSG` only
- Debounce: 500ms after the last event fires the refresh (shorter than the workspace 300ms
  debounce because `.git/` changes are sparse and high-signal)
- `GitWatcher` is created/disposed by `GitViewModel` on repository detect/close
- The existing `RefreshStatusInternalAsync()` + 1-second cooldown handles the actual refresh
  — `GitWatcher` just calls it
- Works alongside the existing `FolderChanged`-triggered refresh (they share the same
  cooldown gate, so two rapid triggers produce one refresh)

**Out of scope:**
- Watching remote refs (no fetch/pull in Phase 7)
- Watching submodule `.git` directories
- Polling fallback for filesystems that don't support `FileSystemWatcher` (inotify limits)

### Key Design Decisions

**D-W1: Watch `.git/` not workspace root**
The existing `IFileSystemWatcherService` watches the workspace root for source file
changes. Watching the workspace root for git changes is too broad — every file save
triggers it. A dedicated `.git/`-scoped watcher fires only on git operations.

**D-W2: Filter to three files**
Only `HEAD`, `index`, and `COMMIT_EDITMSG` are meaningful signals:
- `HEAD` changes on checkout, reset
- `index` changes on stage, unstage, commit
- `COMMIT_EDITMSG` changes on commit

Watching `*` in `.git/` would fire on lock files, gc packs, and other noise.

**D-W3: `GitWatcher` is a plain class, not a service**
It has no business logic — it's a thin wrapper over `FileSystemWatcher` with a debounce
timer and a callback. It does not need DI registration. `GitViewModel` owns its lifetime
directly (create on repo open, dispose on repo close or ViewModel dispose).

**D-W4: Use existing cooldown**
`GitViewModel` already has a 1-second `Stopwatch`-based cooldown on
`RefreshStatusInternalAsync`. `GitWatcher` just invokes a provided `Action` (the refresh
callback). The cooldown gate absorbs any rapid duplicate triggers.

### New Files

```
src/Services/Git/
  GitWatcher.cs    ← FileSystemWatcher wrapper with 500ms debounce; takes Action callback
tests/
  Services/
    GitWatcherTests.cs    ← Debounce logic, start/stop, dispose idempotency
```

### Modified Files

```
src/ViewModels/GitViewModel.cs    ← Create GitWatcher on repo open; dispose on close/dispose
```

### Milestones

| Milestone | Deliverable |
|-----------|-------------|
| M8-W1 | `GitWatcher.cs` + `GitWatcherTests.cs` |
| M8-W2 | `GitViewModel` integration: create/dispose watcher, wire refresh callback |

---

## Combined Milestone Summary

| ID | Deliverable | Dependencies |
|----|-------------|--------------|
| M7-G1 | Graph models + `GetGraphAsync` service method | Baseline complete |
| M7-G2 | `GitGraphViewModel` with lane layout + tests | M7-G1 |
| M7-G3 | `GitGraphControl` rendering + `GitGraphView` + tab | M7-G2 |
| M7-G4 | Commit detail panel | M7-G3 |
| M8-W1 | `GitWatcher` + tests | Baseline complete — ✅ Done |
| M8-W2 | `GitViewModel` watcher integration | M8-W1 — ✅ Done |

M8-W1 and M7-G1 can proceed in parallel (no dependency between them).

---

## Updated Exit Conditions

The Phase 7 exit conditions (all previously met) are supplemented with:

- [ ] Branch graph tab renders DAG with correct lane coloring for local branches
- [ ] Clicking a commit node shows SHA, author, date, message in the detail pane
- [ ] Branch/tag labels visible next to their commit nodes
- [x] Git panel auto-refreshes when external `git commit` / `git checkout` is run
- [x] Auto-reload debounce fires within 1.5 seconds of a `.git/index` or `.git/HEAD` change
- [x] All new tests passing; `dotnet test tests` total = 375 (13 new GitWatcher tests)

---

## Commit Plan

Following `AGENTS.md` one-concern-per-change rule:

1. `git: add GitGraphCommit model and GetGraphAsync to IGitService and LibGit2SharpService`
2. `git: add GitGraphViewModel with lane-assignment algorithm and tests`
3. `git: add GitGraphControl custom renderer and GitGraphView`
4. `git: add GitGraphCommitDetailViewModel and detail pane`
5. `git: add tab switcher (Changes | Graph) to GitPanelView`
6. `git: add GitWatcher with 500ms debounce and tests`
7. `git: wire GitWatcher into GitViewModel for auto-reload`
8. `git: update PHASES.md and EXTENSIONS.md for Phase 7 extensions`
