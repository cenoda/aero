# Phase 7: Git Integration

> Know what changed. Commit with confidence.

## Goal

Add Git panel, diff viewer, and commit UI.

## Entry Condition

- Phase 6 complete (Build & Output)

## Exit Condition

- Git panel shows staged/unstaged changes
- Diff viewer shows inline +/- gutters
- Can stage, unstage, and commit from UI
- Status bar shows current branch
- Modified files show indicators in tabs and file tree

## Checklist

- [ ] **GitRepository** — wrap git CLI or libgit2sharp
- [ ] **Git panel** — staged/unstaged changes list
- [ ] **Diff viewer** — inline diff with +/- gutter
- [ ] Commit UI (message, stage/unstage, commit button)
- [ ] Branch indicator in status bar
- [ ] File modified indicator in editor tab and file tree

## Related Documents

- `docs/LIBRARIES.md` — LibGit2Sharp, DiffPlex
- `docs/architecture/IDE_CORE.md` — Git Integration subsystem
- `docs/design/PANELS_AND_DOCKING.md` — Sidebar panel layout

## Notes

- LibGit2Sharp is preferred over git CLI parsing (typed objects vs string parsing).
- Diff viewer can reuse editor components with custom gutter rendering.
- File modified indicator requires coordination between GitRepository and DocumentManager.
