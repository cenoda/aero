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

## Resolution (Attempt 2 — CORRECT FIX)

**Attempt 1 (FAILED):** Changed `#EditorTabControl` to `$parent[TabControl]` in the binding.
This did not fix the issue because `ItemTemplate` is not the right property.

**Attempt 2 (CORRECT):** Replaced `TabControl.ItemTemplate` with a `Style` that sets
`TabItem.HeaderTemplate`. The `HeaderTemplate` data template is now applied to the
`TabItem.Header` property, which is what the `TabStrip` renders for each tab.

```xml
<!-- Before (incorrect — ItemTemplate targets content, not header) -->
<TabControl.ItemTemplate>
    <DataTemplate>...</DataTemplate>
</TabControl.ItemTemplate>

<!-- After (correct — Style sets HeaderTemplate on TabItem) -->
<TabControl.Styles>
    <Style Selector="TabItem">
        <Setter Property="HeaderTemplate">
            <Setter.Value>
                <DataTemplate>...</DataTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</TabControl.Styles>
```

Also kept the `$parent[TabControl]` fix (from Attempt 1) for the close button command
binding, since `$parent` is more robust than `#controlName` for visual tree traversal.

**File changed:** `src/Views/EditorView.axaml`
**Date:** 2026-06-25
**Status:** resolved

## Key Avalonia TabControl Learning

| Property | What it controls |
|---|---|
| `TabControl.ContentTemplate` | Selected tab's body content |
| `TabControl.ItemTemplate` | Same as ContentTemplate (applied to `ContentControl.Content`) |
| `TabItem.HeaderTemplate` | **Tab header in the strip** (set via Style) |
| `TabItem.Header` | Auto-set to `DataContext` by `UpdateHeader()` if null |
