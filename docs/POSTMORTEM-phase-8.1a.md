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
  - Set IsExpanded = true on all docks
  - Set ActiveDockable on tool docks
  - Set GripMode = Visible
  - Removed unnecessary splitters
  - Verified VisibleDockables had correct count

### Issue 2: Git Panel Empty
- **Symptom:** Git panel shows but content is blank
- **Expected:** Git changes view with staged/unstaged sections

### Issue 3: Editor Not Opening
- **Symptom:** Clicking file in explorer does nothing
- **Expected:** EditorDocument opens with file content

### Issue 4: Bottom Panel Not Responding
- **Symptom:** Problems/Output panels don't respond
- **Expected:** Tab switching works

---

## Root Cause Analysis

### Primary Cause: Dock.Avalonia Internal Behavior Unknown

The fundamental issue is that Dock.Avalonia's internal rendering logic is not transparent. Multiple properties were set correctly (VisibleDockables, IsExpanded, ActiveDockable, GripMode) but the UI did not render as expected.

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