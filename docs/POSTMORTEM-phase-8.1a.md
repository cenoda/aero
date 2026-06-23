# Phase 8.1a Post-Mortem — Dockable Panels Implementation

**Date:** 2026-06-23  
**Outcome:** FAILED - Reverted to M0.5 baseline  
**Branch:** `failed-dockable-panels` (formerly master)

---

## Executive Summary

Phase 8.1a attempted to implement dockable panels using Dock.Avalonia library. The implementation was reverted after multiple issues were discovered that could not be resolved through debugging.

---

## What Was Implemented

### Files Created/Modified

| File | Purpose |
|------|---------|
| `src/Docking/AeroDockFactory.cs` | Layout factory creating dock structure |
| `src/Docking/Model/*.cs` | AeroRootDock, AeroToolDock, AeroDocumentDock, etc. |
| `src/Docking/ToolViewModels/*.cs` | ExplorerTool, GitTool, ProblemsTool, OutputTool |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | Document for editor |
| `src/MainWindow.axaml.cs` | DockControl initialization |
| `src/App.axaml` | DataTemplates for tools/documents |

### Layout Structure

```
Root (Horizontal)
├── LeftColumn (Vertical)
│   └── ToolDock (Left) - Explorer + Git
├── Splitter
└── RightColumn (Vertical)
    ├── DocumentDock - Editor
    ├── Splitter
    └── ToolDock (Bottom) - Problems + Output
```

---

## What Worked

1. **Build succeeded** - All code compiled without errors
2. **DataTemplates wired correctly** - Context set on all tools
3. **ViewModels created** - All 5 ViewModels (Explorer, Git, Problems, Output, Editor) wired
4. **Layout structure correct** - Debug showed all tools in correct positions

---

## What Failed

### Issue 1: Only Explorer Panel Visible
- **Symptom:** Only file tree shows in entire window
- **Expected:** Left sidebar (Explorer + Git), Center (Editor), Bottom (Problems + Output)
- **Debug attempts:**
  1. Set IsExpanded = true on all docks → No change
  2. Set ActiveDockable on tool docks → No change
  3. Set GripMode = Visible → No change
  4. Removed unnecessary splitters → No change
  5. Verified VisibleDockables had correct count (2 for left, 2 for bottom) → Verified OK
  6. Added debug logging to CreateDefaultLayout → Layout structure correct
  7. Verified Context wired to all tools → All 5 tools wired correctly
  8. Checked DataTemplates in App.axaml → All 5 templates present

### Issue 2: Git Panel Empty
- **Symptom:** Git panel shows but content is blank
- **Expected:** Git changes view with staged/unstaged sections
- **Debug attempts:**
  1. Verified GitTool.Context = GitViewModel → Wired correctly
  2. Checked GitPanelView.axaml exists → Exists with content
  3. DataTemplate binds {Binding Context} → Should work

### Issue 3: Editor Not Opening
- **Symptom:** Clicking file in explorer does nothing
- **Expected:** EditorDocument opens with file content
- **Debug attempts:**
  1. Verified EditorDocument.Context = EditorViewModel → Wired correctly
  2. Checked EditorView has open logic → Exists
  3. FileExplorerViewModel has OpenFile command → Exists

### Issue 4: Bottom Panel Not Responding
- **Symptom:** Problems/Output panels don't respond
- **Expected:** Tab switching works
- **Debug attempts:**
  1. Verified ProblemsTool.Context = ProblemsViewModel → Wired correctly
  2. Verified OutputTool.Context = OutputViewModel → Wired correctly
  3. Checked DataTemplates → Both present

---

## Root Cause Analysis

### Primary Cause: Dock.Avalonia Internal Behavior Unknown

The fundamental issue is that Dock.Avalonia's internal rendering logic is not transparent. Multiple properties were set correctly (VisibleDockables, IsExpanded, ActiveDockable, GripMode) but the UI did not render as expected.

### Debugging Attempts (Chronological)

| # | Attempt | Hypothesis | Action | Result |
|---|---------|------------|-------|--------|
| 1 | IsExpanded not set | Set IsExpanded=true on all docks | No change |
| 2 | ActiveDockable not set | Set ActiveDockable on tool docks | No change |
| 3 | GripMode hidden | Set GripMode=Visible | No change |
| 4 | Extra splitter in left column | Removed leftSplitter | No change |
| 5 | Layout structure wrong | Added debug logging | Structure correct |
| 6 | Context not wired | Added debug to WireViewModels | All 5 wired |
| 7 | DataTemplates missing | Checked App.axaml | All 5 present |
| 8 | ViewModels not created | Checked DI registration | All registered |
| 9 | ViewModels null | Checked ShellViewModel constructor | All injected |
| 10 | Views missing | Checked Views folder | All exist |
| 11 | DockControl not initialized | Checked MainWindow code | Initialized correctly |
| 12 | Layout persistence overriding | Checked for saved layouts | None found |
| 13 | Factory not set | Set Factory explicitly | No change |

### Contributing Factors

1. **No Incremental Testing**
   - Entire implementation done in one phase
   - No way to identify when things broke

2. **Library Understanding Insufficient**
   - Dock.Avalonia's layout algorithm not fully understood
   - DeferredContentControl behavior unclear

3. **Plan Without Verification**
   - Implementation plan created but not validated
   - Assumed plan = working code

4. **Multiple Agents, No Coordination**
   - Each agent worked on separate files
   - No end-to-end testing until late

5. **Debug-Friendly Code Missing**
   - No logging from start
   - Hard to trace issues

---

## Sunk Cost

| Item | Cost |
|------|------|
| Commits | ~20+ |
| Time | ~3+ hours debugging |
| Files created | 13+ |
| Lines of code | ~1000+ |

---

## Lessons Learned

### For This Project

1. **Validate plan before implementation**
   - Create minimal proof-of-concept first
   - Test each step before proceeding

2. **Incremental development**
   - One feature at a time
   - Test after each change

3. **Understand the library**
   - Read Dock.Avalonia source/examples
   - Create simple test cases

4. **Debug-friendly code**
   - Add logging from start
   - Make debugging possible

### For Future Phases

1. **Pre-implementation verification**
   - Library works as expected
   - Can render basic layout

2. **Checkpoint system**
   - Regular testing
   - Easy rollback points

3. **Documentation**
   - Document what doesn't work
   - Record debugging attempts

---

## Reverted To

- **Branch:** `master` (now `dockable-panels-v2`)
- **Commit:** `c64e35c` - M0.5 pre-implementation verification
- **State:** Clean baseline, no Docking folder

---

## Next Steps (Recommended)

1. **Study Dock.Avalonia first**
   - Read documentation
   - Create minimal test app

2. **Incremental implementation**
   - M1: Single tool panel works
   - M2: Two tool panels work
   - M3: Document panel works
   - M4: Full layout works

3. **Better debugging**
   - Add logging from start
   - Test frequently

---

## Failed Branch Location

`failed-dockable-panels` - Contains all the failed implementation for reference.