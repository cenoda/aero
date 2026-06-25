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

**Wrong template property:** `TabControl.ItemTemplate` was used to set the tab header,
but `TabItem` extends `HeaderedContentControl` — it has both `Header` and `Content`.

In `TabItem.UpdateHeader()` (triggered by `DataContextProperty.Changed`), when `Header`
is `null`, it calls `SetCurrentValue(HeaderProperty, DataContext)` — setting the entire
`EditorTabViewModel` instance as the header. Since no `HeaderTemplate` was set, Avalonia
calls `ToString()` on it, producing the full type name.

`TabControl.ItemTemplate` maps to the container's **content** area (via
`ItemsControl.PrepareContainerForItemOverride` → `ContentControl.ContentTemplate`),
which is `TabItem.Content` — the body, not the header.

**Both `ItemTemplate` and `ContentTemplate` on TabControl target the same content zone.**
Neither controls the tab header. The header is exclusively controlled by
`TabItem.HeaderTemplate` (from `HeaderedContentControl`).

## Resolution (Attempt 3 — FINAL FIX)

### Attempt 1 (FAILED)
Changed `#EditorTabControl` to `$parent[TabControl]` in the close button binding.
Did not fix — `ItemTemplate` targets content area, not header.

### Attempt 2 (FAILED — committed to origin/master)
Moved the template from `TabControl.ItemTemplate` to a `Style Selector="TabItem"`
setting `HeaderTemplate`. The TabItem template uses `ContentPresenter` with
`ContentTemplate="{TemplateBinding HeaderTemplate}"`, so this should have worked.
But it didn't — possibly a template binding priority issue with the Simple theme.

### Attempt 3 (CURRENT — implicit DataTemplate)
**Root cause confirmed:** `TabItem.UpdateHeader()` sets `Header = DataContext` (the
entire `EditorTabViewModel` instance). The `ContentPresenter` in the TabItem template
renders this Header. With no template matching, Avalonia calls `ToString()` → type name.

**Fix:** Use Avalonia's **implicit typed DataTemplate** system. A `DataTemplate` with
`DataType="vm:EditorTabViewModel"` is placed in `UserControl.DataTemplates`. When the
`ContentPresenter` renders the `Header` (an `EditorTabViewModel`), Avalonia searches
up the resource tree, finds this template by type match, and applies it. No Style,
no `HeaderTemplate` property — pure type-based template resolution.

```xml
<UserControl.DataTemplates>
    <DataTemplate DataType="vm:EditorTabViewModel">
        <StackPanel Orientation="Horizontal" Spacing="4">
            <PathIcon Data="{Binding GlyphGeometry}" .../>
            <TextBlock Text="{Binding GitStatusGlyph}" .../>
            <TextBlock Text="{Binding Title}" .../>
            <Button Command="{Binding $parent[TabControl]..." .../>
        </StackPanel>
    </DataTemplate>
</UserControl.DataTemplates>
```

**File changed:** `src/Views/EditorView.axaml`
**Date:** 2026-06-25
**Status:** resolved

## Key Avalonia TabControl Learning

| What | How |
|---|---|
| `TabItem.Header` | Auto-set to `DataContext` by `UpdateHeader()` |
| Tab item header rendering | `ContentPresenter` with `Content="{TemplateBinding Header}"` and `ContentTemplate="{TemplateBinding HeaderTemplate}"` |
| Implicit typed DataTemplate | Put in `UserControl.DataTemplates`, matches by `DataType`, found via resource tree walk — **most reliable approach** |
| `TabControl.ItemTemplate` | Applied to `TabItem.Content` (body area), NOT header |
| `$parent[TabControl]` | Works for cross-scope bindings from DataTemplates |
