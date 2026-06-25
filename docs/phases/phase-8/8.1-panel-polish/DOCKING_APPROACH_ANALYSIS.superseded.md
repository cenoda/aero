# 8.1a Dockable Panels — Approach Analysis & Problem Log

> **Date:** 2026-06-23  
> **Purpose:** Document which approaches were problematic in the previous implementation attempt after the branch rollback  
> **Target Branch:** `origin/failed-dockable-panels`

---

## 1. What Was Attempted and What Worked

On the `origin/failed-dockable-panels` branch, the full 8.1a implementation was completed.

| Component | File | Status |
|----------|------|------|
| `AeroDockFactory` | `src/Docking/AeroDockFactory.cs` | Build/Tests passed |
| 6 Model classes | `src/Docking/Model/*.cs` | Build/Tests passed |
| 4 Tool ViewModels | `src/Docking/ToolViewModels/*.cs` | Build/Tests passed |
| `EditorDocument` | `src/Docking/DocumentViewModels/` | Build/Tests passed |
| `LayoutPersistenceService` | `src/Services/` | Build/Tests passed |
| `ShellViewModel` toggle commands | | Implemented |
| `MainWindow.axaml` | DockControl layout | Build passed |
| Unit tests | `tests/Docking/` | All 545 passed |

---

## 2. Core Problem: DataTemplate Binding Approach

### 2-1. Original Approach (current `master` branch)

In `MainWindow.axaml`'s `Window.DataTemplates`, each tool/document type was connected to its View by directly referencing `ShellViewModel` properties:

```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView DataContext="{Binding $parent[Window].DataContext.FileExplorerViewModel}"/>
</DataTemplate>
```

This approach **compiles but the binding breaks at runtime.**  
`$parent[Window].DataContext` fails to find the Window when the visual tree is reconstructed inside Dock.Avalonia's `DeferredContentControl`, or the DataContext chain gets overwritten by the tool/document ViewModel itself.

### 2-2. Fix Attempted in `failed-dockable-panels`

The fix removed bindings from DataTemplates and instead injected ViewModels directly into the `Context` property from code-behind:

```xml
<!-- No DataContext binding in DataTemplate -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — WireViewModels()
case ExplorerTool explorer:
    explorer.Context = shell.FileExplorerViewModel;
    break;
```

`Context` is a property on `IDockable`, and `DeferredContentControl` uses this value as the DataContext when rendering the DataTemplate.

After this fix, logs showed Context was correctly injected on all tools, and folder open also worked (`FileExplorerViewModel.LoadFolderAsync` call confirmed).

### 2-3. However, Remaining Problem: Panel Rendering

Even with successful Context injection, the following issues occurred:

- **Only Explorer panel fills the entire screen** — The left sidebar, editor center, and bottom panel should all appear separately, but only Explorer renders
- Git, Problems, Output panels exist but don't appear on screen
- Editor area is empty

This is a problem of **Dock.Avalonia's internal layout rendering behavior differing from expectations.**
Despite 13+ debugging attempts, the root cause could not be pinpointed:

| Attempt | Hypothesis | Result |
|---------|-----------|--------|
| Set `IsExpanded = true` | Tool dock is collapsed | No change |
| Set `ActiveDockable` | No active tab | No change |
| Set `GripMode = Visible` | Grip is hidden | No change |
| Removed left column splitter | Tree structure issue | No change |
| Set Factory explicitly | Disconnected from DockControl | No change |
| Checked layout save files | Previous layout overriding | Confirmed no save file |

---

## 3. Root Cause (Estimated)

### 3-1. Missing `Dock.Avalonia.Themes.Simple`

One of the commit logs from `failed-dockable-panels` was:

```
fix(docking): add missing Dock.Avalonia.Themes.Simple package
```

Without its base theme, Dock.Avalonia's internal `DockControl` controls won't render or will have zero size. Without the `Dock.Avalonia.Themes.Simple` package:

- Each panel container's size is calculated as 0
- ProportionalDock's splitter doesn't work
- ToolDock's tab strip doesn't render

If `App.axaml` only has `<SimpleTheme />` or `<FluentTheme />` but lacks `<dock:DockTheme/>` or `<StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/..."/>`, the entire dock layout may appear empty.

**It is critical to verify whether this theme is included in `App.axaml` on the current `master`.**

### 3-2. `InitializeDockControl` Timing Issue

A timing issue was discovered in the final implementation of `failed-dockable-panels`:

- **Original code (`master`):** `InitializeDockControl()` called in `MainWindow` constructor
  -> `DataContext` not yet set, so `WireViewModels()` gets null for `shell.FileExplorerViewModel` etc.
- **Fixed code:** `InitializeDockControl(shell)` called when `Initialize(IMessageBus bus)` is invoked (= after `DataContext` is set)

The current `master` branch still uses the **original constructor-time call approach.**
The `$parent[Window].DataContext` binding was a workaround to resolve the timing issue late in the DataTemplate, but it doesn't work inside Dock.Avalonia's `DeferredContentControl`.

---

## 4. Current `master` Branch Status & To-Do

The current `master` uses the DataTemplate approach from `failed-dockable-panels`, but whether Context injection works correctly at runtime has not been verified.

Items to verify before restarting:

```
[ ] Does App.axaml include Dock.Avalonia theme styles?
    -> Install Dock.Avalonia.Themes.Simple package + add StyleInclude to App.axaml
[ ] Is ShellViewModel null when initializing DockControl in MainWindow constructor?
    -> Move InitializeDockControl() inside Initialize()
[ ] Does the $parent[Window].DataContext binding in DataTemplate actually work
    inside Dock's DeferredContentControl?
    -> Switching to direct Context property injection is safer
[ ] Are Proportion values set on ProportionalDock children?
    -> Without them, all children divide equally or become 0
```

---

## 5. Recommended Re-implementation Direction

### DataTemplate Approaches (Two Options)

**Option A — Direct Context Injection (safe, verified in `failed-dockable-panels`)**
```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
    <!-- No DataContext binding — Context injected from code-behind -->
</DataTemplate>
```
```csharp
// Inside Initialize() (after DataContext is set)
explorer.Context = shell.FileExplorerViewModel;
```

**Option B — ReactiveUI Binding (alternative)**
```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView DataContext="{Binding Context}"/>
    <!-- References IDockable.Context property — no parent chain -->
</DataTemplate>
```

Option B is more declarative and MVVM-aligned,
but requires verification that Dock.Avalonia guarantees timing of setting Context as DataContext at DataTemplate render time.

### Initialization Order (Must Follow)

```csharp
// App.axaml.cs — Set DataContext
window.DataContext = shell;

// MainWindow.Initialize() — bus subscription + DockControl initialization
public void Initialize(IMessageBus bus, ShellViewModel shell)
{
    // 1. Subscribe to bus
    _bus = bus;
    // ...

    // 2. Initialize DockControl (DataContext already set, so shell is accessible)
    InitializeDockControl(shell);
}

private void InitializeDockControl(ShellViewModel shell)
{
    DockControl.InitializeFactory = true;   // Before assigning Layout
    DockControl.InitializeLayout = false;
    DockControl.Factory = layout.Factory!;

    shell.ActiveLayout = layout;
    WireViewModels(layout, shell);          // Inject Context

    DockControl.Layout = layout;            // Last
}
```

---

## 6. Summary

| Problem | Cause | Solution |
|---------|-------|----------|
| Panel content empty | `$parent[Window].DataContext` binding breaks inside Dock | Replace with direct `Context` injection |
| Initialization timing | `InitializeDockControl()` called in constructor before `DataContext` is set | Move inside `Initialize()` |
| Layout filled by Explorer only | Missing `Dock.Avalonia.Themes.Simple` (suspected) | Add theme package + StyleInclude in App.axaml |
| Proportion not set | `ProportionalDock` children lack `Proportion` values | Explicitly set left 0.25, right 0.75, etc. |
