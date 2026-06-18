# Phase 2: File Explorer & Project System

> See your files. Open your folder. Work with your project.

## Goal

Add a file explorer panel and project recognition so the IDE feels like a workspace.

## Entry Condition

- Phase 1 complete (editor with tabs and open/save)

## Exit Condition

- Can open a folder and see its tree structure
- File tree auto-refreshes on external changes
- Project files (.sln, .csproj, package.json) are recognized
- Context menu works for New/Rename/Delete
- Clicking a file opens it in the editor

## Checklist

- [x] **FileExplorer panel** — tree view of a folder
- [x] **Open Folder** — File → Open Folder, populates the tree
- [x] **FileSystemWatcher** — auto-refresh on external changes
- [x] **ProjectLoader** — recognize .sln, .csproj, package.json
- [x] Context menu: New File, New Folder, Delete, Rename
- [x] Click file in tree → opens in editor

## Related Documents

- `docs/architecture/IDE_CORE.md` — Project System subsystem
- `docs/design/PANELS_AND_DOCKING.md` — panel layout and lifecycle

## Notes

- FileSystemWatcher can be noisy; debounce or throttle refresh.
- ProjectLoader is lightweight at this stage — just recognition, not full parsing.
- Tree view should show file icons (use Material.Icons.Avalonia).
