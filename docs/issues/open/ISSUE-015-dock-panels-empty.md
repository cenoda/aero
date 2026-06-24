# ISSUE-015: Git Panel Doesn't Load in Dock Panels (Explorer Works)

## Description

Dock panel Explorer shows the file tree correctly, but the Git panel doesn't load any content (staged/unstaged changes, branch info, etc.). The sidebar versions of both panels work correctly, but the dock Git panel is empty or non-functional.

## Expected vs Actual

- **Expected**: Dock Git panel should show staged/unstaged files, current branch, and Git graph (same as sidebar)
- **Actual**: Dock Git panel content area is empty / doesn't display Git data. Explorer dock panel works correctly.

## Debug Log

### Attempt 1 — Investigate Dock.Avalonia rendering pipeline
- **Hypothesis:** DockControl's default `AutoCreateDataTemplates="True"` creates `IToolContent → ToolContentControl` templates that intercept tools before our Application-level DataTemplates.
- **Action:** Studied Dock.Avalonia source code. Found `DockDataTemplateHelper.CreateDefaultDataTemplates()` adds templates for `IToolContent`, `IDocumentContent`, etc.
- **Result:** Confirmed default templates could intercept. However, the more likely issue is the `{Binding Context}` circular binding (see Attempt 2).

### Attempt 2 — Analyze {Binding Context} circular binding
- **Hypothesis:** `DataContext="{Binding Context}"` on the root element of a DataTemplate creates a circular binding.
- **Action:** Traced Avalonia binding lifecycle. When DataTemplate root gets DataContext=Tool, `{Binding Context}` evaluates to Tool.Context=ViewModel. Then DataContext changes to ViewModel, triggering re-evaluation against ViewModel (which has no Context property), setting DataContext to null.
- **Result:** CONFIRMED as root cause. The circular DataContext rebinding kills the value.

### Attempt 3 — Study Dock.Avalonia QuickStart sample
- **Hypothesis:** QuickStart sample shows correct pattern.
- **Action:** Studied `DockQuickStartSample/MainWindow.axaml`. Uses DocumentTemplate with Grid root + ContentControl child. ContentControl is NOT the root, so parent DataContext is stable.
- **Result:** Pattern identified — need to avoid `{Binding Context}` on template root's DataContext.

### Attempt 4 — Implement ContentControl wrapper with ContentTemplate
- **Hypothesis:**
  1. `AutoCreateDataTemplates="False"` prevents default templates.
  2. `Content="{Binding Context}"` + `ContentTemplate` avoids circular binding: ContentControl.DataContext = Tool (stable), Content = Tool.Context (one-way, non-self-referential), ContentTemplate creates View with ViewModel as DataContext.
- **Action:** Added `AutoCreateDataTemplates="False"` to DockSpikeControl; changed all DataTemplates to use ContentControl/ContentTemplate pattern; added `xmlns:vm` namespace.
- **Result:** Build succeeds, 535 tests pass. Pending UI verification.

### Attempt 5 — Re-evaluate after UI verification: Explorer works, Git doesn't
- **Observation:** After Attempt 4 fix (ContentTemplate pattern), Explorer panel shows the file tree correctly. Git panel still doesn't load.
- **Key observation:** Both `ExplorerTool` and `GitTool` use identical `DataContext="{Binding Context}"` pattern in `App.axaml`. Both go through the same `WireViewModels` code path. If the binding fix resolved Explorer, it should have resolved Git too — unless the issue is specific to `GitViewModel` initialization or data loading.
- **Hypotheses to investigate:**
  1. `GitViewModel` may not be properly registered in DI, so `WireViewModels` sets `Context` to null or throws.
  2. `GitViewModel` may be registered but doesn't load data until certain conditions are met (e.g., git repo detection, async init not awaited, git executable not found on Linux).
  3. The `GitPanelView` x:DataType binding expects `GitViewModel` but the Context is a different type (or null).
  4. Nested DataTemplates in `GitPanelView` (e.g., `GitFileStatusViewModel`) use `$parent[UserControl]` which may resolve differently inside a Dock template hierarchy vs sidebar.
  5. `GitViewModel`'s `StagedChanges`/`UnstagedChanges` collections may remain empty due to git service not being initialized in the dock path.

## Root Cause (Final)

**UPDATED 2026-06-25:** The original root cause (primary) below was correct and fixed Explorer, but Git still doesn't load. The issue is now narrowed to a Git-specific problem, not a general DataTemplate binding issue. See Attempt 5 above.

**ORIGINAL (partially resolved):**

**Primary:** `DataContext="{Binding Context}"` on the DataTemplate root creates a circular binding:

1. View.DataContext = Tool (set by Avalonia template machinery)
2. `{Binding Context}` evaluates → Tool.Context = ViewModel
3. View.DataContext = ViewModel
4. DataContext CHANGE triggers re-evaluation of `{Binding Context}`
5. Now evaluates against ViewModel → no "Context" property → null
6. View.DataContext = null → **panel content empty**

**Secondary:** DockControl's `AutoCreateDataTemplates="True"` creates default templates that could also intercept, but primary circular binding would still exist.

## Resolution

### Applied (Explorer fixed — M2)

| File | Change |
|------|--------|
| `src/MainWindow.axaml` | Added `AutoCreateDataTemplates="False"` to DockSpikeControl |
| `src/App.axaml` | Changed from `DataContext="{Binding Context}"` to `Content="{Binding Context}"` + ContentTemplate |
| `src/App.axaml` | Added `xmlns:vm="using:Aero.ViewModels"` namespace |

### Pending (Git still broken)

Git panel investigation needed — likely a GitViewModel initialization, DI registration, or git-service-not-found-on-Linux issue.

## Status

| Date | Status |
|------|--------|
| 2026-06-24 | Explorer fixed; Git still empty (original report was imprecise — Explorer works, Git doesn't) |
| 2026-06-25 | Doc updated to reflect actual state: Explorer works, Git broken. Debug pending. |