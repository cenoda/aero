# 8.1a — Dockable Panels (Freeform Mode): Implementation Plan

> **Status:** Ready for implementation (2026-06-23)
> **Branch:** `phase-8.1a-dockable-panels-v2`
> **Replaces:** Failed v1 (see `docs/POSTMORTEM-phase-8.1a.md`)
> **Depends on:** 8.9 Design System ✅, 8.5 Icon Decision ✅, Phase 7 Git ✅

---

## 0. Lessons from v1 Failure

The v1 implementation compiled cleanly, wired all five tools correctly, and still produced a blank window. After 13+ debug rounds over 3+ hours, the root cause was never conclusively isolated — every layer was a candidate because too many unverified assumptions landed at once: a custom factory, five dockables, a five-zone layout, deferred Context injection, and an unverified theme URI.

**What changed in v2:**

| v1 Anti-Pattern | v2 Response |
|----------------|------------|
| Grid replaced in a single cutover | `LayoutMode` switch keeps Grid alive through M5 |
| Factory + models + wiring in one commit | M0.5 proves rendering works in pure XAML before any C# |
| No logging | `[Dock]`-prefixed debug output from M1 |
| "Assumed the plan works" | Each milestone has an explicit pass/fail gate |
| Theme URI guessed | M0.5 verifies the exact URI against installed package |

---

## 1. Entry Gates (M0)

All must be true before M0.5 starts:

- [x] `dotnet build src/aero.csproj` — 0 errors (confirmed 2026-06-23)
- [x] `dotnet test tests` — 527 passed
- [x] `src/Docking/` does not exist (clean baseline after v1 revert)
- [x] Dock.Avalonia packages installed: `Dock.Avalonia 11.3.*`, `Dock.Avalonia.Themes.Simple 11.3.*`, `Dock.Serializer.SystemTextJson 11.3.*`
- [x] No Dock references in `App.axaml` or `MainWindow.axaml`

---

## 2. Architecture

### 2.1 DataTemplate Strategy: Option A (Direct Context Injection)

**Chosen unanimously by all agents. Verified working in v1 post-mortem logs.**

The Views (`FileExplorerView`, `GitPanelView`, etc.) remain unchanged. `DataTemplate`s registered in `App.axaml` map each tool/document type to its View. The `Context` property on each `IDockable` is set from code-behind in `WireViewModels()` — no `{Binding}` gymnastics, no `$parent[Window].DataContext`.

```xml
<!-- App.axaml — DataTemplates (added in M1) -->
<Application.DataTemplates>
    <DataTemplate DataType="dockTools:ExplorerTool"><views:FileExplorerView/></DataTemplate>
    <DataTemplate DataType="dockTools:GitTool"><views:GitPanelView/></DataTemplate>
    <DataTemplate DataType="dockTools:ProblemsTool"><views:ProblemsView/></DataTemplate>
    <DataTemplate DataType="dockTools:OutputTool"><views:OutputView/></DataTemplate>
    <DataTemplate DataType="dockDocs:EditorDocument"><views:EditorView/></DataTemplate>
</Application.DataTemplates>
```

```csharp
// MainWindow.axaml.cs — WireViewModels()
private void WireViewModels(AeroRootDock layout, ShellViewModel shell)
{
    foreach (var dockable in EnumerateDockables(layout))
    {
        switch (dockable)
        {
            case ExplorerTool t:   t.Context = shell.FileExplorerViewModel; break;
            case GitTool t:        t.Context = shell.GitViewModel;          break;
            case ProblemsTool t:   t.Context = shell.ProblemsViewModel;     break;
            case OutputTool t:     t.Context = shell.OutputViewModel;       break;
            case EditorDocument d: d.Context = shell.EditorViewModel;       break;
        }
        Log($"[Dock] Wired {dockable.GetType().Name}.Context");
    }
}
```

### 2.2 Dock.Avalonia API Map (Verified Against v11.3.12.1)

| Interface/Class | Namespace | Notes |
|----------------|-----------|-------|
| `IFactory` | `Dock.Model.Core` | Implemented by `AeroDockFactory`. Methods return interface types. |
| `Factory` | `Dock.Model` | Abstract base class. Extend this instead of implementing from scratch. |
| `IDockable` | `Dock.Model.Core` | Base interface. Has `Id`, `Title`, `Context`, `Proportion`, `Dock`, `Owner`, `Factory`, `IsVisible`. |
| `IRootDock` | `Dock.Model.Controls` | Top-level container. Has `Window`, `Windows`, `IsFocusableRoot`. |
| `IProportionalDock` | `Dock.Model.Controls` | Adds `Orientation` to `IDockable`. Proportions via `IDockable.Proportion` (inherited). |
| `IProportionalDockSplitter` | `Dock.Model.Controls` | Splitter between proportional children. Has `CanResize`. |
| `IToolDock` | `Dock.Model.Controls` | Container for tools. Has `Alignment` (Left/Right/Top/Bottom/Unset), `IsExpanded`, `ActiveDockable`. |
| `IDocumentDock` | `Dock.Model.Controls` | Container for documents. Has `CanCreateDocument`, `CreateDocument`, `LayoutMode`, `TabsLayout`. |
| `ITool` | `Dock.Model.Controls` | Interface for side/panel dockables. |
| `IDocument` | `Dock.Model.Controls` | Interface for content/document dockables. |
| `DockControl` | `Dock.Avalonia.Controls` | Root TemplatedControl. Properties: `Layout`, `Factory`, `InitializeLayout`, `InitializeFactory`, `IsDockingEnabled`. |
| `Alignment` enum | `Dock.Model.Core` | Values: `Unset`, `Left`, `Bottom`, `Right`, `Top`. |
| `DockSerializer` | `Dock.Serializer.SystemTextJson` | `Serialize<T>()`, `Deserialize<T>()`, `Load<T>()`, `Save<T>()`. |

**Key correction from v1:** `Proportion` lives on `IDockable` (base), not on `IProportionalDock`. All model classes inherit it.

### 2.3 LayoutMode Switch (From Opus — The Architectural Breakthrough)

```
┌─────────────────────────────────────────────────────────────────┐
│  LayoutMode.Grid (default through M5)                          │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Current Grid layout — unchanged, always works           │  │
│  └───────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│  LayoutMode.Freeform (M3+, hidden by default)                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  DockControl — built incrementally, verified per milestone│  │
│  └───────────────────────────────────────────────────────────┘  │
│  Grid stays alive as fallback. DockControl only becomes        │
│  default in M6 after full verification.                        │
└─────────────────────────────────────────────────────────────────┘
```

### 2.4 Layout Tree (M1)

```
AeroRootDock
└── AeroProportionalDock (Horizontal)
    ├── AeroProportionalDock (Vertical, Proportion=0.22)
    │   └── AeroToolDock (Alignment=Left)
    │       ├── ExplorerTool    (Context = FileExplorerViewModel)
    │       └── GitTool         (Context = GitViewModel)
    ├── AeroProportionalDockSplitter
    └── AeroProportionalDock (Vertical, Proportion=0.78)
        ├── AeroDocumentDock (Proportion=0.72)
        │   └── EditorDocument (Context = EditorViewModel)
        ├── AeroProportionalDockSplitter
        └── AeroToolDock (Alignment=Bottom, Proportion=0.28)
            ├── ProblemsTool    (Context = ProblemsViewModel)
            └── OutputTool      (Context = OutputViewModel)
```

**Proportion constants:**
```csharp
internal static class DockProportions
{
    public const double LeftSidebar = 0.22;
    public const double CenterStack = 0.78;
    public const double EditorRow   = 0.72;
    public const double BottomRow   = 0.28;
}
```

### 2.5 Initialization Sequence

```
App.axaml.cs: OnFrameworkInitializationCompleted()
│
├─ 1. Resolve ShellViewModel, IMessageBus, LSPManager, GitViewModel
├─ 2. Create MainWindow { DataContext = shell }     ← DataContext BEFORE Initialize
├─ 3. mainWindow.Initialize(bus)                     ← wires bus + dock
└─ 4. desktop.MainWindow = mainWindow

MainWindow.axaml.cs: Initialize(IMessageBus bus)
│
├─ A. Read DataContext → assert ShellViewModel
├─ B. Subscribe to MessageBus (existing handlers)
└─ C. InitializeDockControl(shell)                     ← only if LayoutMode == Freeform

MainWindow.axaml.cs: InitializeDockControl(ShellViewModel shell)
│
├─ 1. var factory = new AeroDockFactory()
├─ 2. DockControl.InitializeFactory = true            ← BEFORE Layout (triggers locators)
├─ 3. DockControl.InitializeLayout = false            ← prevent double init
├─ 4. DockControl.Factory = factory                   ← safety net
├─ 5. var layout = factory.CreateDefaultLayout()      ← calls InitLayout internally
├─ 6. WireViewModels(layout, shell)                   ← Context injection BEFORE layout
├─ 7. DockControl.Layout = layout                     ← LAST — triggers rendering
└─ 8. Log: "init complete, N dockables"
```

**Critical ordering:** `InitializeFactory = true` MUST be set before `DockControl.Layout = layout`. The `OnPropertyChanged` handler fires `Initialize()` when Layout is set, which checks `InitializeFactory` to set up `ContextLocator`, `HostWindowLocator`, etc. Without locators, drag-and-drop and rendering fail silently.

### 2.6 Theme Include (Verified Against Package)

The theme URI was wrong in the previous plan (`SimpleDockTheme.xaml`). The actual embedded resource is `DockSimpleTheme.axaml`. Since `DockSimpleTheme` is a `ControlTheme` (not a `ResourceDictionary`), the include mechanism needs verification in M0.5.

```
avares://Dock.Avalonia.Themes.Simple/DockSimpleTheme.axaml
```

```xml
<!-- App.axaml — Application.Styles -->
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
    <!-- M0.5 gate: must render the spike tab or this URI is wrong -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/DockSimpleTheme.axaml" />
    <StyleInclude Source="avares://aero/Styles/ControlThemes.axaml" />
</Application.Styles>
```

---

## 3. Scope

### In scope (8.1a only)

- Dock.Avalonia infrastructure: `DockControl`, factory, model classes
- 5 panel dockable wrappers: Each existing panel gets an `ITool`/`IDocument` implementation
- Drag-and-drop rearrangement between docking zones
- Panel visibility toggling via existing View menu commands
- `LayoutMode` switch: `Grid` (default) / `Freeform` (parallel)
- Layout persistence via `Dock.Serializer.SystemTextJson`
- Default flip to Freeform in M6

### Out of scope (deferred)

| Feature | Phase | Reason |
|---------|-------|--------|
| Tile Mode (8.1b) | 8.1b | Different layout engine entirely |
| Tear-Away Windows (8.1c) | 8.1c | Requires OS window management |
| `IDockingService` abstraction | 8.1b | Only one concrete impl; premature per AGENTS.md §4 |
| Option B (`{Binding Context}`) | Future | Unverified; Option A proven working |
| Removing ShellViewModel booleans | Never | Depended on by tests, persistence, Grid fallback |
| DialogHost.Avalonia | Avalonia 12 | Incompatible with 11.3 |
| New panels | 8.1c+ | Only existing 5 converted |

---

## 4. Milestones

### M0.5 — Pure-XAML Rendering Spike

**Goal:** Prove Dock.Avalonia renders inside our app before writing any C# model code.

**Steps:**
1. Add theme `StyleInclude` to `App.axaml` (see §2.6)
2. Add a new tab "Dock spike" in the existing `MainWindow.axaml` sidebar `TabControl`
3. Verify the theme URI by inspecting the installed package:
   ```bash
   unzip -l ~/.nuget/packages/dock.avalonia.themes.simple/11.3.12.1/lib/net8.0/*.dll 2>/dev/null | grep -i theme
   ```

**Verification:**
- `dotnet build src/aero.csproj` — 0 errors
- App starts, click "Dock spike" tab → see *any* non-empty docked content
- **If the spike tab is empty, STOP.** Theme URI, package version, or Avalonia 11.3 compat is wrong.

**Rollback:** `git tag v2-m0.5-spike`

---

### M1 — Model Classes + Factory

**Goal:** Replace M0.5 inline XAML with factory-driven layout, still inside the spike tab.

**Files to create:**

| File | Purpose |
|------|---------|
| `src/Docking/LayoutMode.cs` | `enum LayoutMode { Grid, Freeform }` |
| `src/Docking/AeroDockFactory.cs` | `Factory` subclass |
| `src/Docking/Model/AeroRootDock.cs` | `IRootDock` implementation |
| `src/Docking/Model/AeroProportionalDock.cs` | `IProportionalDock` implementation |
| `src/Docking/Model/AeroProportionalDockSplitter.cs` | `IProportionalDockSplitter` implementation |
| `src/Docking/Model/AeroToolDock.cs` | `IToolDock` implementation |
| `src/Docking/Model/AeroDocumentDock.cs` | `IDocumentDock` implementation |
| `src/Docking/ToolViewModels/ExplorerTool.cs` | `ITool` — File Explorer |
| `src/Docking/ToolViewModels/GitTool.cs` | `ITool` — Git Panel |
| `src/Docking/ToolViewModels/ProblemsTool.cs` | `ITool` — Problems |
| `src/Docking/ToolViewModels/OutputTool.cs` | `ITool` — Output |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | `IDocument` — Editor |

**Steps:**
1. Create all model classes implementing `INotifyPropertyChanged`
2. Create `AeroDockFactory : Factory` with `CreateDefaultLayout()` building the tree from §2.4
3. Each model class gets `Equals`/`GetHashCode` overrides using `Id`
4. Add `Application.DataTemplates` in `App.axaml` (see §2.1)
5. Replace M0.5 inline XAML with `<dock:DockControl x:Name="DockSpike"/>`
6. Wire factory + layout in `MainWindow.axaml.cs` (see §2.5)
7. Add `[Dock]`-prefixed debug logging

**Verification:**
- Spike tab shows layout driven by factory
- Debug output prints layout tree depth-first
- `dotnet test tests` — 527 pass

**Rollback:** `git tag v2-m1-factory`

---

### M2 — Wire Real ViewModels

**Goal:** Spike tab shows real content — file tree, Git panel, editor, problems, output.

**Steps:**
1. Implement `WireViewModels()` (see §2.1) in `MainWindow.axaml.cs`
2. `EnumerateDockables()` recursively walks `IRootDock` via `IDock.Dockables`
3. Each tool's `Context` is set to the corresponding ShellViewModel property

**Verification:**
- Spike tab: Explorer tree expands, Git shows changes, Problems lists diagnostics, Output shows build log
- Click file in Explorer → opens in Editor within the spike tab
- `dotnet test tests` — 527 pass

**Rollback:** `git tag v2-m2-wired`

---

### M3 — Promote DockControl to Window Region (LayoutMode Switch)

**Goal:** DockControl available alongside the existing Grid. Mode switchable from View menu.

**Steps:**
1. Add `LayoutMode` property to `ShellViewModel`
2. Add "Layout Mode" menu item under View
3. In `MainWindow.axaml`: Grid (existing) + DockControl, controlled by `IsVisible`
4. On mode switch: if switching to Freeform and dock not initialized, call `InitializeDockControl()`
5. Remove the M1/M2 spike tab

**Verification:**
- App starts in Grid mode (unchanged behavior)
- View → Freeform → dock layout appears, all 5 panels visible
- View → Grid → original layout returns
- Both modes use the same `ShellViewModel`
- `dotnet test tests` — 527 pass

**Rollback:** `git tag v2-m3-mode-switch`

---

### M4 — Toggle-Command Parity

**Goal:** All existing View menu commands and keyboard shortcuts work in both modes.

**Hide/show semantics:**

| Action | Grid Mode (unchanged) | Freeform Mode |
|--------|----------------------|---------------|
| Hide sidebar | `IsSidebarVisible = false` | Remove left `IToolDock` from parent's `VisibleDockables`; remember insertion index |
| Show sidebar | `IsSidebarVisible = true` | Re-insert at remembered index, restore Proportion |
| Switch sidebar tab | `ActiveSidebarTabIndex = 1` | `leftToolDock.ActiveDockable = leftToolDock.VisibleDockables[1]` |
| Toggle Output | `IsBottomPanelVisible = true; ActiveBottomTabIndex = 1` | Ensure bottom `IToolDock` in `VisibleDockables`; set `ActiveDockable = outputTool` |
| Toggle Problems | `IsBottomPanelVisible = true; ActiveBottomTabIndex = 0` | Same, target ProblemsTool |

**Steps:**
1. Toggle commands read/write `ShellViewModel` booleans FIRST (source of truth)
2. In Freeform mode, additionally push state to dock model via `SyncDockVisibility()`
3. Helper methods `FindDockable()`, `FindToolDock()` walk `IRootDock.Dockables` recursively
4. All keybindings unchanged

**Verification:**
- Freeform mode: View menu toggles hide/show correct panels
- `Ctrl+OemTilde` toggles Output
- Grid mode: all toggles work as before
- `dotnet test tests` — 527+ pass

**Rollback:** `git tag v2-m4-toggles`

---

### M5 — Layout Persistence

**Goal:** Dock arrangement persists across restarts.

**Files to create:**

| File | Purpose |
|------|---------|
| `src/Services/LayoutPersistenceService.cs` | `ILayoutPersistenceService` + implementation |

**Steps:**
1. Implement `LayoutPersistenceService`: save path `~/.aero/layout.json`
2. Serialization via `DockSerializer<IRootDock>` with `System.Text.Json`
3. Atomic write: `.tmp` file then `File.Move(overwrite: true)`
4. Corrupt file handling: try/catch → delete → return null → fallback to default
5. Schema version field in JSON for forward-compat
6. Register in DI (singleton)
7. Save on `MainWindow.OnClosing`; load only when `LayoutMode == Freeform`

**Verification:**
- Launch → Freeform → rearrange → close → relaunch → preserved
- Corrupt JSON → relaunch → "Layout reset" status, default loaded
- Grid mode → no layout file loaded
- `dotnet test tests` — 527+ pass

**Rollback:** `git tag v2-m5-persist`

---

### M6 — Default Flip + Cleanup

**Goal:** Freeform becomes the default. Phase 8.1a complete.

**Steps:**
1. Change `LayoutMode` default from `Grid` to `Freeform`
2. Create `manual_test_phase8_1a.sh` with smoke checklist
3. Update `docs/roadmap/PHASES.md` — mark 8.1a complete
4. Record scope reductions in `docs/TOFIX.md`
5. Clean up unused imports and debug code

**Verification:**
- `dotnet build src/aero.csproj` — 0 errors, 0 new warnings
- `dotnet test tests` — all pass (≥527)
- `manual_test_phase8_1a.sh` passes smoke checklist
- `docs/roadmap/PHASES.md` 8.1a checked off

**Rollback:** `git tag v2-m6-default-freeform`

---

## 5. Files Summary

### Files to Create

| File | Milestone | Purpose |
|------|-----------|---------|
| `src/Docking/LayoutMode.cs` | M1 | `enum LayoutMode { Grid, Freeform }` |
| `src/Docking/AeroDockFactory.cs` | M1 | `Factory` subclass — creates all dock model types |
| `src/Docking/Model/AeroRootDock.cs` | M1 | `IRootDock` implementation |
| `src/Docking/Model/AeroProportionalDock.cs` | M1 | `IProportionalDock` implementation |
| `src/Docking/Model/AeroProportionalDockSplitter.cs` | M1 | `IProportionalDockSplitter` implementation |
| `src/Docking/Model/AeroToolDock.cs` | M1 | `IToolDock` implementation |
| `src/Docking/Model/AeroDocumentDock.cs` | M1 | `IDocumentDock` implementation |
| `src/Docking/ToolViewModels/ExplorerTool.cs` | M1 | `ITool` — File Explorer |
| `src/Docking/ToolViewModels/GitTool.cs` | M1 | `ITool` — Git Panel |
| `src/Docking/ToolViewModels/ProblemsTool.cs` | M1 | `ITool` — Problems |
| `src/Docking/ToolViewModels/OutputTool.cs` | M1 | `ITool` — Output |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | M1 | `IDocument` — Editor |
| `src/Services/LayoutPersistenceService.cs` | M5 | Layout save/restore |

### Files to Modify

| File | Milestone | Changes |
|------|-----------|---------|
| `src/App.axaml` | M0.5 | Add Dock theme `StyleInclude` |
| `src/App.axaml` | M1 | Add `Application.DataTemplates` for tools/documents |
| `src/MainWindow.axaml` | M0.5 | Add "Dock spike" tab (temporary) |
| `src/MainWindow.axaml` | M3 | Add Grid/DockControl switch structure |
| `src/MainWindow.axaml.cs` | M1 | Add `InitializeDockControl()`, `WireViewModels()`, `EnumerateDockables()` |
| `src/MainWindow.axaml.cs` | M3 | Wire LayoutMode switch |
| `src/MainWindow.axaml.cs` | M4 | Add `SyncDockVisibility()`, `FindDockable()` |
| `src/MainWindow.axaml.cs` | M5 | Wire layout persistence on close |
| `src/ViewModels/ShellViewModel.cs` | M3 | Add `LayoutMode` property, `IsFreeformMode` computed |
| `src/ViewModels/ShellViewModel.cs` | M4 | Rewrite toggle commands to operate per mode |
| `src/ViewModels/ShellViewModel.cs` | M6 | Change default to Freeform |
| `src/App.axaml.cs` | M5 | Register `LayoutPersistenceService` in DI |

### Files NOT to Modify

- `src/ViewModels/EditorViewModel.cs` — tab management unchanged
- `src/ViewModels/FileExplorerViewModel.cs` — file tree logic unchanged
- `src/ViewModels/GitViewModel.cs` — git operations unchanged
- `src/ViewModels/ProblemsViewModel.cs` — diagnostic display unchanged
- `src/ViewModels/OutputViewModel.cs` — build output unchanged
- `src/Services/DocumentManager.cs` — document lifecycle unchanged
- `src/Views/*.axaml` — all View files unchanged (Context injection means zero View changes)

---

## 6. Logging Strategy

Channel: `System.Diagnostics.Debug.WriteLine` with `[Dock]` prefix. Info-level messages also published via `IMessageBus.Publish(new StatusMessage(...))` for Output panel visibility.

| Site | Level | Message |
|------|-------|---------|
| `AeroDockFactory.CreateDefaultLayout()` | Debug | Tree dump: depth, type, id, proportion per child |
| `AeroDockFactory.GetDockable()` | Debug | Each `ITool`/`IDocument` constructed |
| `MainWindow.InitializeDockControl()` | Info | "init: begin / factory / layout built / wired / layout assigned" |
| `MainWindow.WireViewModels()` | Debug | "Wired {Type}.Context → {ContextType}" per dockable |
| `MainWindow.OnClosing()` | Debug | Layout JSON length + path |
| `LayoutPersistenceService.Load()` | Info | "loaded N bytes" or "default layout (reason: ...)" |

---

## 7. Key Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Theme URI wrong | Medium | Critical | M0.5 verifies by inspecting package; spike empty → STOP |
| Dock.Avalonia 11.3 undocumented behavior | Medium | High | M0.5 is purely XAML against exact installed version |
| Grid replacement breaks keybindings | Medium | Medium | LayoutMode switch keeps Grid alive; M4 wires per mode |
| Layout JSON corrupts on shutdown | Medium | High | M5 try/catch + version check; atomic write; delete corrupt |
| Context injection too late for initial render | Low | Critical | WireViewModels runs strictly before Layout assignment |
| Toggle commands drift between modes | Medium | Medium | Reactive booleans remain canonical; mode code pushes to dock |

---

## 8. Definition of Done

All must be true before declaring 8.1a complete:

- [ ] `dotnet build src/aero.csproj` — 0 errors, 0 new warnings
- [ ] `dotnet test tests` — all existing tests pass (≥527)
- [ ] `manual_test_phase8_1a.sh` passes smoke checklist
- [ ] `LayoutMode` defaults to `Freeform`; switch to Grid and back works
- [ ] All 5 panels render real content in Freeform mode
- [ ] All keyboard shortcuts still fire
- [ ] `docs/roadmap/PHASES.md` 8.1a checked off
- [ ] `docs/TOFIX.md` has no unchecked items blocking 8.1a
- [ ] Any scope reductions recorded with rationale
