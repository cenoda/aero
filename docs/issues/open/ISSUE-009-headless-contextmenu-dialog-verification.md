# ISSUE-009: Cannot verify M4 context-menu → dialog flow under headless Xvfb

**Label:** CHORE
**Status:** open
**Priority:** medium
**Reported by:** review session (Phase 2 M4 post-merge visual verification)
**Assigned to:** —
**Related:** Phase 2 M4 (commits `5c3afa2`, `20ca707`), `manual_test_phase2_m4.sh`

## Description

The Phase 2 M4 context-menu operations (New File / New Folder / Rename / Delete)
and their dialogs (`TextInputDialog`, `ConfirmDialog`) are covered by 219 unit
tests at the ViewModel level, but the **interactive rendering path** — right-click
a tree node → context menu popup → click "New File" → dialog appears → type →
submit → tree updates — has not been visually verified.

An earlier session claimed this flow passed, but its screenshots actually showed
an empty app with no folder open (no tree nodes, so nothing to right-click). That
verification was invalid; its artifacts were deleted.

A corrected `manual_test_phase2_m4.sh` was written that opens a seeded folder via
the M3 CLI startup-folder argument (`aero <dir>`) so the tree is populated. The
tree now loads correctly, but driving the **context menu popup** via `xdotool`
under a window-manager-less Xvfb does not work reliably.

## Steps to Reproduce

1. `bash manual_test_phase2_m4.sh`
2. Inspect `manual_test_screenshots/phase2_m4_02_context_menu.png`.

**Expected:** the per-node context menu popup is visible over the tree.
**Actual:** the node is selected (highlighted) but no context menu popup renders;
the new file is never created on disk (`WARN: created_by_test.cs not found`).

## Notes / Screenshots

- `phase2_m4_01_tree_loaded.png` — ✅ tree populated (`src`, `README.md`, "2 entries"). Startup-folder path works.
- `phase2_m4_02_context_menu.png` — ❌ node selected, no context menu popup.
- `phase2_m4_03_new_file_dialog.png` (attempt 1) — showed the menu-BAR File menu, not the dialog (stray keystrokes).

## Debug Log

> Recognized as a failure loop after 2 attempts (per AGENTS.md §6). Stopped
> tweaking and filed this issue instead of a 3rd pixel-coordinate guess.

### Attempt 1
- **Hypothesis:** Right-click at sidebar (70, 58) lands on the first tree node and opens its ContextMenu; keyboard Down+Enter selects "New File".
- **Action:** `xdotool mousemove --window 70 58; click 3; key Down; key Return`. Full-screen capture via `import -window root`.
- **Result:** `src` was selected, but no context menu appeared. The Down/Return keystrokes (with no context menu focused) ended up opening the menu-bar **File** menu. No file created.
- **Error / Output:** `phase2_m4_02` shows selected `src`, no popup. `phase2_m4_03` shows the menu-bar File menu. `WARN: created_by_test.cs not found`.

### Attempt 2
- **Hypothesis:** y=58 was above the node content; the ContextMenu is attached to the inner StackPanel, so the click must land on the glyph/name. Move to (55, 78) and click the menu item by mouse instead of keyboard.
- **Action:** `xdotool mousemove --window 55 78; click 3; mousemove 115 92; click 1`.
- **Result:** y=78 now selected the **second** row (`README.md`), not `src` — row coordinate model is unreliable. Still no context menu popup rendered. No file created.
- **Error / Output:** `phase2_m4_02` shows `README.md` selected, no popup. `WARN: created_by_test.cs not found`.

### Root-cause analysis (why stop here)
- Incremental coordinate tweaking is not converging and is exactly the failure
  loop AGENTS.md §6 warns against.
- The deeper cause is a **test-harness limitation**, not (necessarily) a code
  bug: Avalonia context menus and dialogs are separate top-level popup surfaces.
  Under Xvfb with **no window manager**, synthetic `xdotool` right-click → popup
  timing and popup window mapping are unreliable, and exact per-row pixel
  coordinates are fragile.
- What IS reliably verified headlessly: the app launches, the startup-folder
  argument opens a folder, and the tree renders its nodes. What is NOT: popup
  menu rendering and modal-dialog interaction.

## Resolution

- **Root cause:** (preliminary) headless Xvfb + xdotool is the wrong tool for
  verifying Avalonia popup/dialog interaction; not confirmed to be a product bug.
- **Recommended fix / path forward:**
  1. Reduce `manual_test_phase2_m4.sh` to what it can reliably assert
     (launch + startup-folder tree load) and mark the context-menu/dialog steps
     as **manual, real-display only**.
  2. Do a one-time manual verification on a real display (right-click a node,
     run all four operations) to confirm the interactive flow — the ViewModel
     logic is already unit-tested, so this is a visual/rendering confirmation.
  3. Longer term, adopt **Avalonia.Headless** (in-process UI test harness with
     bitmap rendering) for interactive UI verification instead of Xvfb+xdotool.
     Track as a separate CHORE if pursued.
- **Commit:** —
- **Closed date:** —
