# ISSUE-011 — Toggle Dock Spike Doesn't Work + Editor Unscrollable

> **Status:** in-progress
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

TBD after deeper investigation