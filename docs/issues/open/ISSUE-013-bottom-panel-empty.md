# ISSUE-013: Bottom panel appears empty/white with no content

**Label:** BUG
**Status:** open
**Priority:** medium
**Reported by:** user
**Assigned to:**
**Related:** 

## Description

The bottom panel (Problems + Output tabs) appears as blank white space when opened. No content is displayed - neither the Problems tab nor the Output tab show any content. The panel header with tabs appears to work (can switch between Problems and Output), but the content area is empty.

## Steps to Reproduce

1. Open the application
2. Open a folder (Ctrl+O or menu)
3. Toggle the bottom panel (View menu or keyboard shortcut)
4. Observe the bottom panel area

**Expected behavior:** The bottom panel should show either:
- Problems tab: List of diagnostics (errors/warnings) or empty state "No problems detected"
- Output tab: Command output area with toolbar or empty state "No output yet"

**Actual behavior:** The panel content area is completely white/empty. No content visible.

## Notes / Screenshots

### Investigation Notes

**File locations:**
- MainWindow.axaml lines 164-224: Bottom panel container with TabControl
- Views/OutputView.axaml: Output panel with empty state UI
- Views/ProblemsView.axaml: Problems panel with empty state UI
- ViewModels/OutputViewModel.cs: Output panel logic
- ViewModels/ProblemsViewModel.cs: Problems panel logic

**Structure:**
```
Border (IsVisible, Height binding)
  Grid
    Border (header with title + collapse button)
    TabControl (Problems + Output tabs)
      TabItem Problems -> ProblemsView
      TabItem Output -> OutputView
```

**Empty state implementations:**
- ProblemsView.axaml lines 38-52: StackPanel with "No problems detected" message
- OutputView.axaml lines 50-66: StackPanel with "No output yet" message

**Possible causes:**
1. Dynamic resources not defined (panel.background, panel.emptyStateForeground)
2. DataContext not properly set on child views
3. Empty state visibility binding issue
4. ItemsControl/ListBox not rendering

### Code Review Findings

**OutputView.axaml** - Uses dynamic resources:
- `{DynamicResource panel.background}` - Border background
- `{DynamicResource panel.border}` - Border
- `{DynamicResource editor.background}` - Toolbar
- `{DynamicResource panel.emptyStateForeground}` - Empty state text
- `{DynamicResource editor.foreground}` - Output text

**ProblemsView.axaml** - Uses dynamic resources:
- `{DynamicResource panel.background}` - Border background
- `{DynamicResource panel.border}` - Border
- `{DynamicResource panel.sectionBackground}` - Header
- `{DynamicResource panel.emptyStateForeground}` - Empty state text

**Hypothesis 1:** Dynamic resources missing from theme
**Action:** Check if panel.* resources are defined in Themes/
**Result:** Not yet verified

**Hypothesis 2:** DataContext not propagating to child views
**Action:** Check if DataContext bindings in MainWindow.axaml are correct
**Result:** MainWindow.axaml uses `DataContext="{Binding OutputViewModel}"` on OutputView - appears correct

**Hypothesis 3:** Empty state StackPanel always visible (IsVisible binding inverted)
**Action:** Check IsVisible binding on empty state StackPanels
**Result:** Both use `IsVisible="{Binding !Lines.Count}"` / `IsVisible="{Binding !Diagnostics.Count}"` - this should work if Count returns int

## Debug Log

> **Rule:** If the fix is not obvious in 2 attempts, record everything here.

### Attempt 1
- **Hypothesis:** Dynamic resources missing
- **Action:** Check Themes/ folder for panel.* resource definitions
- **Result:** Pending

### Attempt 2
- **Hypothesis:** DataContext not set on child views
- **Action:** Verify OutputViewModel/ProblemsViewModel instances are created in ShellViewModel
- **Result:** Pending

### Attempt 3
- **Hypothesis:** Empty state visibility binding issue (Count property returns 0 but binding fails)
- **Action:** Check how Lines.Count and Diagnostics.Count are implemented
- **Result:** Pending

## Resolution

- **Root cause:**
- **Fix:**
- **Commit:**
- **Closed date:**