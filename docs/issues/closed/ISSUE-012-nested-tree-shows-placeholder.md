# ISSUE-012 — Nested Folders Show "…" Placeholder Instead of Real Files

> **Status:** closed
> **Created:** 2026-06-24
> **Priority:** high
> **Milestone:** Phase 8.1a (Dockable Panels) — found during testing

---

## Description

Files inside nested folders (folders inside folders inside the root) display as "…" (the
placeholder character `\u2026`) and are unopenable. First-level directories expand correctly,
but second-level and deeper directories never load their children.

---

## Root Cause Analysis

### Initial hypothesis (wrong): Shared singleton placeholder
The first fix made `PlaceholderChild` a factory method (`CreatePlaceholderChild()`), preventing
the same instance from being added to multiple `ObservableCollection`s. This was correct but
did NOT fix the nested-folder issue.

### Actual root cause: `ContainerPrepared` not firing for nested TreeViewItems

The view code-behind uses `TreeView.ContainerPrepared` to attach `Expanded` handlers to
each `TreeViewItem`:

```csharp
tree.ContainerPrepared += OnTreeContainerPrepared;
```

In Avalonia, `TreeView.ContainerPrepared` only fires for **direct** containers of the
`TreeView` — not for nested `TreeViewItem`s created by `TreeDataTemplate` at deeper levels.

This means:
- ✅ `level1`'s TreeViewItem gets the `Expanded` handler → expanding works
- ❌ `level2`'s TreeViewItem does NOT get the `Expanded` handler → expanding does nothing
- ❌ `level3`'s TreeViewItem does NOT get the `Expanded` handler → same

When the user expands `level2`, Avalonia shows the placeholder child ("…") but never calls
`EnsureChildrenLoadedAsync`, so real children are never loaded.

### Fix: Recursively hook `ContainerPrepared` on each nested `TreeViewItem`

`TreeViewItem` inherits from `ItemsControl` which also has `ContainerPrepared`. By
subscribing to `ContainerPrepared` on each `TreeViewItem` as well as the root `TreeView`,
the handler propagates to all nesting levels:

```csharp
private void OnTreeContainerPrepared(object? sender, ContainerPreparedEventArgs e)
{
    if (e.Container is TreeViewItem item)
    {
        item.Expanded -= OnItemExpanded;
        item.Expanded += OnItemExpanded;
        // Recursively hook nested TreeViewItems
        item.ContainerPrepared -= OnTreeContainerPrepared;
        item.ContainerPrepared += OnTreeContainerPrepared;
    }
}
```

---

## Debug Log

### Attempt 1 (2026-06-24)
- **Hypothesis:** Shared singleton `PlaceholderChild` caused "…" to appear in nested dirs
- **Action:** Changed `PlaceholderChild` from static field to `CreatePlaceholderChild()` factory
- **Result:** Build succeeded, tests pass, but user reports bug still not fixed
- **Error:** N/A — correct fix but wrong root cause

### Attempt 2 (2026-06-24)
- **Hypothesis:** `ContainerPrepared` only fires for root-level TreeViewItems
- **Action:** Recursively hook `ContainerPrepared` on each `TreeViewItem` so nested items
  also get the `Expanded` handler
- **Result:** Pending user verification

---

## Required Investigation

1. Verify `TreeView.ContainerPrepared` fires for nested items (or doesn't)
2. Write a ViewModel-level test to prove `EnsureChildrenLoadedAsync` works for deep trees
3. If VM test passes, issue is confirmed to be in the View layer

---

## Related Files

- `src/Views/FileExplorerView.axaml.cs` — `OnTreeContainerPrepared`, `OnItemExpanded`
- `src/ViewModels/FileExplorerViewModel.cs` — `EnsureChildrenLoadedAsync`
- `src/ViewModels/FileExplorerNodeViewModel.cs` — `CreatePlaceholderChild()`

---

## Resolution

**Root Cause:** `TreeView.ContainerPrepared` only fires for direct `TreeViewItem`s. Nested items (level 2+) are created by `TreeDataTemplate` inside each `TreeViewItem`, so `ContainerPrepared` fires on the parent `TreeViewItem`, not the root `TreeView`. The `Expanded` handler was never attached to deeper levels.

**Fix:** Recursively subscribe to `ContainerPrepared` on each `TreeViewItem` in `FileExplorerView.axaml.cs`:
```csharp
item.ContainerPrepared -= OnTreeContainerPrepared;
item.ContainerPrepared += OnTreeContainerPrepared;
```

**Also:** Previous singleton placeholder fix (`CreatePlaceholderChild()` factory) was correct but insufficient on its own.

**Closed:** 2026-06-24 — confirmed working by user.
