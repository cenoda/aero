# ISSUE-011 — Toggle Dock Spike Doesn't Work + Editor Unscrollable

> **Status:** closed
> **Created:** 2026-06-24
> **Priority:** high
> **Milestone:** Phase 8.1a (Dockable Panels)

---

## Description

Two bugs reported:
1. **Toggle doesn't work:** Ctrl+Shift+D or View → Toggle Dock Spike menu item doesn't activate the dock spike
2. **Editor unscrollable:** After toggling off (or if toggle fails), the main editor becomes unscrollable

---

## Debug Log

### Attempt 1 (2026-06-24)

**Hypothesis:** Toggle command not executing or _mainWindow reference null

**Action:** Added detailed logging to ToggleSpikeCommand, AssignSpikeLayout, ClearSpikeLayout

**Result:** Build succeeded, but user reports bug still not fixed

**Error / Output:** N/A — logging added but issue persists

---

### Attempt 2 (202ypothesis:** Focus not restored to editor after toggle off, blocking scroll

**Action:** 
- Added FocusEditor() method to EditorViewModel
- Added FocusEditorRequested event
- EditorView subscribes and calls _activeEditor.Focus() via dispatcher
- ClearSpikeLayout calls FocusEditor() after clearing

**Result:** Build succeeded, tests pass, but user reports bug still not fixed

**Error / Output:** N/A — issue persists

---

### Attempt 3 (Current — Creating Issue)

**Hypothesis:** The bug is deeper — possibly:
- KeyBinding not being recognized
- Command binding broken
- ReactiveCommand not executing
- DockSpikeControl not rendering even when IsSpikeActive=true

**Action:** Creating issue file for deeper investigation

---

## Root Cause (Unknown)

The toggle simply doesn't work. Possible root causes:
1. KeyBinding (Ctrl+Shift+D) not being captured by the Window
2. ToggleSpikeCommand not being invoked (ReactiveCommand issue)
3. IsSpikeActive property change not triggering UI update
4. DockSpikeControl.IsVisible binding not working
5. DockSpikeControl itself failing to render

---

## Required Investigation

1. Run the app and check Debug output for `[Dock] ToggleSpikeCommand EXECUTED` — does it appear?
2. Check if IsSpikeActive actually changes (add breakpoint or log)
3. Check if DockSpikeControl becomes visible (add breakpoint on IsVisible setter)
4. Check if AssignSpikeLayout is called
5. Check if DockControl renders at all (add visual logging)

---

## Related Files

- `src/ViewModels/ShellViewModel.cs` — ToggleSpikeCommand
- `src/MainWindow.axaml.cs` — AssignSpikeLayout, ClearSpikeLayout
- `src/MainWindow.axaml` — DockSpikeControl XAML
- `src/Docking/AeroDockFactory.cs` — Layout creation
- `docs/phases/phase-8/8.1-dockable-panels/TOFIX.md` — T2.4

---

## Resolution

**Root Cause:** `AeroDockFactory.CreateDefaultLayout()` was missing the critical `Factory.InitLayout(root)` call. Without this, DockControl receives a layout object but has no idea how to render it — resulting in blank white space.

**What was wrong:**
1. The layout tree was correctly assembled (RootDock → ProportionalDock → Tools/Editors)
2. But `Factory.InitLayout()` was never called — this is the Dock.Avalonia call that wires up navigation, `CanClose`, `CanDrag`, `Navigate` commands, active dockable state, and all rendering machinery
3. Additionally, visibility was controlled via XAML bindings (`IsVisible="{Binding !IsSpikeActive}"`) which Avalonia doesn't support (no `!` negation). Fixed by moving visibility control to code-behind (`SetSpikeVisibility()`)
4. Layout assignment now happens BEFORE visibility toggle (timing fix)

**Fix applied:**
1. Added `Factory.InitLayout(root)` at the end of `CreateDefaultLayout()` in `AeroDockFactory.cs`
2. Moved visibility control from XAML bindings to code-behind (`MainWindow.axaml.cs`)
3. Added `DebugLogger` utility class for GUI debugging
4. Added `IsNotSpikeActive` computed property to `ShellViewModel`

**Closed:** 2026-06-24

**Note:** User reports additional bug found during testing (not blocking this fix).