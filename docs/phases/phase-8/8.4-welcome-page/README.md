# 8.4 — Welcome Page

**Goal:** A landing tab shown when no files are open, replacing the empty gray area.

**Scope:**
- Shows recent folders (read from workspace persistence store — **8.7**)
- Quick-action buttons: New File, Open Folder, Open File
- Click recent folder → open workspace; click quick-action → invoke command
- Simple layout, no complex widgets
- Empty state when no recent folders exist (first launch)

**Dependencies:**
- **8.7 Workspace Persistence** — provides the recent folders list. The welcome page can be built with an empty state and wired to 8.7's data once available.
- **8.9 Design System** — spacing, typography, button styles
- **8.2 Theme Engine** — color tokens for welcome page specific tokens (welcome.background, welcome.button.hoverBackground, etc.)

**Exit condition:** Welcome tab appears on startup with working recent folders and quick actions.

**Tests:**
- Unit: Empty state renders when no recent folders exist
- Unit: Clicking a recent folder invokes FolderOpened with correct path
- Integration: Folder opened from welcome page is added to recent list
- Manual: Welcome page is the default tab when no files are open

