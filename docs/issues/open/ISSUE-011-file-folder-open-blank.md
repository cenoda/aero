# ISSUE-011: File/Folder Open Shows Blank Loading State

## Description

When opening a file (Ctrl+O) or folder (Ctrl+Shift+O), the panels show blank/loading state instead of the file tree or editor content.

**Expected:** File Explorer populates with folder contents; Editor shows file content
**Actual:** Panels remain blank/empty

## Debug Log

### Attempt 1 (2026-06-23)
- **Hypothesis:** WireViewModels() not called for loaded layouts
- **Action:** Fixed MainWindow.axaml.cs to call WireViewModels() for both default AND loaded layouts
- **Result:** No change - still blank
- **Error / Output:** User reports "nothing have changed"

### Attempt 2 (2026-06-23)
- **Hypothesis:** Context binding syntax might be wrong
- **Action:** Tried removing DataContext binding, binding to self, adding debug output
- **Result:** No change - still blank
- **Error / Output:** Debug output shows Context IS being set correctly

### Attempt 3 (2026-06-23)
- **Hypothesis:** Need more investigation
- **Action:** Created issue file for tracking

### Attempt 4 (2026-06-23)
- **Hypothesis:** Context IS set, but folder not opened
- **Action:** User confirmed status bar shows "No folder open"
- **Result:** Context IS set correctly - issue is FolderOpened message not working
- **Error / Output:** "No folder open" = Context set, but FolderOpened not received

### Attempt 5 (2026-06-23)
- **Hypothesis:** FileExplorerViewModel not subscribed to FolderOpened
- **Action:** Added eager resolution of FileExplorerViewModel in App.axaml.cs
- **Result:** Still same - "No folder open"
- **Error / Output:** Build succeeded, but issue persists

### Attempt 6 (2026-06-23)
- **Hypothesis:** File picker dialog not working in environment
- **Action:** Added debug output to trace message flow
- **Result:** No debug output - message not being published OR dialog not returning
- **Error / Output:** No [DEBUG] messages in terminal

### Attempt 7 (2026-06-23)
- **Hypothesis:** File picker dialog returns no results
- **Action:** Try CLI argument to bypass dialog
- **Result:** DBus crash - environment issue
- **Error / Output:** TaskCanceledException from Tmds.DBus

## Root Cause

**Environment issue, not code bug.** The system cannot run Avalonia file picker dialogs due to DBus/Tmds issues. This is a Linux desktop environment limitation, not a code defect.

The app crashes when trying to use:
1. File → Open Folder (Ctrl+Shift+O) - file picker dialog
2. CLI argument - also triggers DBus internally

### Attempt 8 (2026-06-23)
- **Hypothesis:** WireViewModels() runs in constructor BEFORE DataContext is set
- **Action:** Changed InitializeDockControl() to take ShellViewModel parameter, call from Initialize() after DataContext set
- **Result:** ✅ FIXED - Debug shows all tools wired correctly
- **Error / Output:** 
  ```
  [MainWindow] Wired ExplorerTool.Context
  [MainWindow] Wired GitTool.Context
  [MainWindow] Wired EditorDocument.Context
  [MainWindow] Wired ProblemsTool.Context
  [MainWindow] Wired OutputTool.Context
  [FileExplorerViewModel] Received FolderOpened: /home/cenoda/aero/
  [FileExplorerViewModel] LoadFolderAsync complete: 17 entries
  ```

## Resolution

**FIXED** - Root cause was timing issue in MainWindow initialization.

**Root Cause:** `InitializeDockControl()` was called in the MainWindow constructor BEFORE `DataContext` was set (in App.axaml.cs line 52), so `WireViewModels()` couldn't access the ShellViewModel to wire up the Context properties.

**Fix Applied:**
1. Changed `InitializeDockControl()` to take a `ShellViewModel` parameter
2. Moved the call to `Initialize()` which runs AFTER DataContext is set
3. This ensures `WireViewModels()` has access to the ShellViewModel when wiring Context

**Commit:** `fix(docking): wire ViewModels after DataContext is set`

## Investigation Notes

1. **Context IS being set** - Debug output confirms WireViewModels() runs and sets Context on all tools

2. **If panels show "No folder open"** - Context IS set, but LoadFolderAsync() isn't working

3. **If panels are completely empty** - Context might NOT be set (different bug)

4. **File open flow:**
   - `ShellViewModel.OpenFileCommand` → `OpenFileAsync()` → `_bus.Publish(new FolderOpened(normalizedPath))`
   - Status bar should show "Opened folder: /path" when folder opens

5. **Folder open flow:**
   - `ShellViewModel.OpenFolderCommand` → `OpenFolderAsync()` → `_bus.Publish(new FolderOpened(normalizedPath))`
   - `FileExplorerViewModel` subscribes to `FolderOpened` → calls `LoadFolderAsync(msg.Path)`
   - Status bar should show "Loading /path…" then "N entries"

## Questions for User

1. What does the status bar show when you press Ctrl+Shift+O and select a folder?
   - "Opened folder: /path" = message published
   - "Loading /path…" = LoadFolderAsync started
   - "N entries" = LoadFolderAsync completed
   - Something else = check further

2. What does the File Explorer panel show?
   - "No folder open. Use File → Open Folder" = Context IS set, HasRootPath=false
   - Completely blank = Context NOT set (different bug)
   - "Loading /path…" = LoadFolderAsync running

3. Can you open the app with a CLI argument?
   - `dotnet run --project src -- /home/cenoda/aero`
   - This bypasses the file picker and directly opens the folder

## Files Checked

- `src/MainWindow.axaml.cs` - WireViewModels() logic ✓
- `src/App.axaml` - DataTemplates ✓
- `src/Docking/ToolViewModels/ExplorerTool.cs` - Context property from base class ✓
- `src/Docking/DocumentViewModels/EditorDocument.cs` - Context property from base class ✓

## Resolution

Pending user feedback on status bar content.