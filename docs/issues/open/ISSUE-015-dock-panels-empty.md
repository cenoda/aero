# ISSUE-015: Dock Panels Show But Content Is Empty

## Description

Dock panels (Explorer, Git) show in the dock UI but content is empty. The sidebar versions work correctly, but the dock versions don't display any content.

## Expected vs Actual

- **Expected**: Dock panels should show file tree (Explorer) and staged/unstaged files (Git)
- **Actual**: Dock panels show but content areas are empty

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

## Root Cause (Final)

**Primary:** `DataContext="{Binding Context}"` on the DataTemplate root creates a circular binding:

1. View.DataContext = Tool (set by Avalonia template machinery)
2. `{Binding Context}` evaluates → Tool.Context = ViewModel
3. View.DataContext = ViewModel
4. DataContext CHANGE triggers re-evaluation of `{Binding Context}`
5. Now evaluates against ViewModel → no "Context" property → null
6. View.DataContext = null → **panel content empty**

**Secondary:** DockControl's `AutoCreateDataTemplates="True"` creates default templates that could also intercept, but primary circular binding would still exist.

## Resolution

| File | Change |
|------|--------|
| `src/MainWindow.axaml` | Added `AutoCreateDataTemplates="False"` to DockSpikeControl |
| `src/App.axaml` | Changed from `DataContext="{Binding Context}"` to `Content="{Binding Context}"` + ContentTemplate |
| `src/App.axaml` | Added `xmlns:vm="using:Aero.ViewModels"` namespace |

## Status

| Date | Status |
|------|--------|
| 2026-06-24 | fixed (pending UI verification) |