# ISSUE-009: M4 context-menu → dialog flow not verifiable under headless Xvfb (harness limitation)

**Label:** CHORE
**Status:** closed
**Priority:** medium
**Reported by:** review session (Phase 2 M4 post-merge visual verification)
**Assigned to:** —
**Related:** Phase 2 M4 (commits `5c3afa2`, `20ca707`), `manual_test_phase2_m4.sh`

## Description

The Phase 2 M4 context-menu operations (New File / New Folder / Rename / Delete)
and their dialogs (`TextInputDialog`, `ConfirmDialog`) are covered by unit tests
at the ViewModel level, but the **interactive rendering path** — right-click a
tree node → context menu popup → click "New File" → dialog appears → type →
submit → tree updates — has not been confirmed end-to-end in a trustworthy UI
environment.

An earlier session claimed this flow passed, but its screenshots actually showed
an empty app with no folder open (no tree nodes, so nothing to right-click). That
verification was invalid; its artifacts were deleted.

A corrected `manual_test_phase2_m4.sh` now limits its automated assertions to
what is reliably verifiable under Xvfb: app launch, startup-folder opening, and
tree rendering. The script explicitly leaves the context-menu/dialog flow to a
manual real-display checklist.

What remains unresolved is whether the missing popup/dialog verification is only
a **test-harness limitation** or whether there is also a **product defect** in
the interactive flow. The current issue stays open until that ambiguity is
resolved.

## Steps to Reproduce

Historical failed harness reproduction:

1. Run the earlier Xvfb + `xdotool` popup-driving attempt against the seeded
  Phase 2 M4 workspace.
2. Observe that the tree node becomes selected but no reliable context-menu
  popup/dialog interaction occurs.

**Expected:** the per-node context menu popup is visible over the tree and can
be used to open the corresponding dialog.
**Actual:** under the old Xvfb + `xdotool` attempt, the node is selected
(highlighted) but no context menu popup renders; the new file is never created
on disk (`WARN: created_by_test.cs not found`).

## Notes / Screenshots

- `phase2_m4_tree_loaded.png` — ✅ tree populated (`src`, `README.md`, "2 entries"). Startup-folder path works.
- Prior popup/dialog screenshots from the failed `xdotool` attempts document the harness limitation, not a confirmed product defect.

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

**Root cause (confirmed):** a **test-harness limitation**, not a product defect.
Xvfb without a window manager + `xdotool` synthetic pointer automation cannot
reliably drive Avalonia's separate popup/dialog top-level surfaces. The Phase 2
M4 interactive flow itself is correct.

**Verification performed (2026-06-18):** manual real-display run via
`dotnet run --project src -- <seed-folder>`, exercising the full checklist in
`manual_test_phase2_m4.sh`. All six checks passed:

1. ✅ Right-click a node (file and directory) shows the context menu.
2. ✅ New File → dialog → name → file appears in tree and on disk.
3. ✅ New Folder → dialog → folder appears in tree.
4. ✅ Rename (F2) → dialog pre-filled with current name → rename applies.
5. ✅ Delete (Del) → confirm dialog → node removed from tree.
6. ✅ Deleting a file open in a tab leaves the editor tab/buffer intact (R1.4).

(One reported "directory right-click fails" observation during the session was a
touchpad mis-click, not a defect — re-tested and passes.)

**Outcome:**
- The product flow is verified correct; relabelled `BUG` → `CHORE` (this was a
  verification gap, not a bug).
- `manual_test_phase2_m4.sh` already limits its automated assertions to
  launch + startup-folder tree load and prints the manual checklist for the rest.
- Durable, repeatable automated coverage is tracked separately in **ISSUE-010**
  (adopt `Avalonia.Headless` UI harness). That work is intentionally out of
  scope here.

## Acceptance Criteria

- [x] `manual_test_phase2_m4.sh` only asserts headless-verifiable behavior
    (launch + startup-folder tree load).
- [x] The issue document explicitly states that Xvfb + `xdotool` is a harness
    limitation, not proof of a product defect.
- [x] The interactive context-menu/dialog flow is verified by a trustworthy
    method (manual real-display check, 2026-06-18 — all 6 checks passed).
- [x] The verified outcome is recorded and the label/status are updated to match
    the evidence (`BUG` → `CHORE`, closed).
- [x] Any follow-up infrastructure work is split into a separate issue if it is
    broader than resolving this verification ambiguity (→ ISSUE-010).

## Recommended Next Step

Preferred long-term direction: adopt an in-process Avalonia UI harness
(`Avalonia.Headless`, likely with a separate `aero.UiTests` project) so the flow
can be verified deterministically and protected against regression.

That work is **not** assumed complete by this issue and requires explicit
approval because it adds dependencies and likely a new test project.

- **Commit:** —
- **Closed date:** 2026-06-18 (resolved as harness limitation; product flow verified manually)
