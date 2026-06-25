---
title: Editor Tab shows type name instead of filename
severity: high
priority: high
frequency: always
description: |
  When opening a file in the editor, the tab header displays "Aero.ViewModels.EditorTabViewModel" 
  (the full type name) instead of showing the actual filename (e.g., "program.cs").
components:
  - src/Views/EditorView.axaml
  - src/ViewModels/EditorTabViewModel.cs
  - src/Models/Editor/TextDocument.cs
tags:
  - editor
  - tabs
  - binding
status: closed
---

## Steps to Reproduce
1. Launch the application
2. Open any file (Ctrl+O or File > Open)
3. Observe the tab header

## Expected Behavior
The tab should display the filename (e.g., "program.cs") with a close button.

## Actual Behavior
The tab displays "Aero.ViewModels.EditorTabViewModel" (the type name).

## Investigation Notes
- The Title property in EditorTabViewModel correctly returns `_document.DisplayName`
- The XAML template uses `{Binding Title}` which should work
- Previous fix attempts:
  1. Removed x:DataType from DataTemplate (did not fix)
  2. Added FallbackValue (did not fix)

## Root Cause

**Binding scope failure:** The `#EditorTabControl` name-scope reference in the close button's
`Command` binding was unresolvable from within the `TabControl.ItemTemplate` DataTemplate.

In Avalonia, `TabItem` containers auto-generated from `ItemsSource` live inside the `TabStrip`
which is within the `TabControl`'s `ControlTemplate` — a **different name scope** from the
host `UserControl`. The `#controlName` syntax can only resolve controls in the same name scope.
When the binding fails at runtime, the entire header DataTemplate renders nothing, and the
`TabControl` falls back to displaying `ToString()` on the item — producing the full type name
`"Aero.ViewModels.EditorTabViewModel"` instead of the filename.

**All other aspects were correct:**
- `TabControl.ItemTemplate` IS the header template in Avalonia 11 (confirmed by official docs)
- `TabControl.ContentTemplate` is for the content area
- `EditorTabViewModel.Title` correctly returns `_document.DisplayName`
- The other bindings (`{Binding Title}`, `{Binding GlyphGeometry}`, etc.) were fine

## Resolution

Changed the close button command binding from name-scope reference to **visual tree ancestor lookup**:

```diff
- Command="{Binding #EditorTabControl.DataContext.CloseTabCommand}"
+ Command="{Binding $parent[TabControl].DataContext.CloseTabCommand}"
```

`$parent[TabControl]` walks up the visual tree to find the `TabControl` ancestor,
bypassing name scope limitations. The `.DataContext.CloseTabCommand` path then
resolves to `EditorViewModel.CloseTabCommand` correctly.

**File changed:** `src/Views/EditorView.axaml` line 29
**Date:** 2026-06-25
**Status:** resolved
