# Phase 8.1a — Consolidated Implementation Plan

> **Source:** Multi-agent analysis (Minimax, Deepseek, Claude, Hy3, Blackbox, Opus, Qwen)
> **Date:** 2026-06-23
> **Branch:** `phase-8.1a-dockable-panels-v2`

---

## Executive Summary

All 7 agents unanimously agree on core technical decisions (Option A Context injection, DataContext-first initialization, explicit proportions, M0.5 proof-of-concept). The standout architectural insight from Opus is the **LayoutMode switch** — keeping the existing Grid layout working as a fallback while building DockControl in parallel, then switching default only after full verification in M6.

---

## Consensus Findings (Unanimous)

| Decision | Verdict | Source |
|----------|---------|--------|
| **DataTemplate Strategy** | **Option A** — Direct `IDockable.Context` injection from code-behind | All 7 |
| **Initialization Order** | `Set DataContext → Initialize() → DockControl` (NOT constructor) | All 7 |
| **Explicit Proportions** | Every `ProportionalDock` child must have explicit `Proportion` | All 7 |
| **M0.5 First** | Proof-of-concept before any real implementation code | 6/7 |
| **Logging from Start** | `[Dock]`-prefixed output from M1 onwards | All 7 |
| **Keep ShellViewModel Booleans** | `IsSidebarVisible`, `IsBottomPanelVisible` etc. must remain | Claude, Opus, Minimax |
| **No $parent[Window].DataContext** | Must not use parent-relative bindings in DataTemplates | All 7 |

---

## Key Innovation: LayoutMode Switch (from Opus)

> **Problem with v1:** The entire Grid → DockControl replacement was a single cutover. When it broke, everything broke — no fallback.

> **Solution:** Introduce a `LayoutMode` setting (`Grid` / `Freeform`) that lets both layouts coexist. Grid remains default through M3. DockControl is built in parallel, verified incrementally, and only becomes the default in M6.

This converts the v1 "big bang" into a safe, revertible migration with a known-good fallback at every step.


## Milestone Plan

| # | Milestone | Scope | Verification | Key Files |
|---|-----------|-------|-------------|-----------|
| **M0** | **Baseline Gate** | Confirm clean tree: build passes, tests pass, no Docking/ folder | `dotnet build`, `dotnet test`, verify no dock code | — |
| **M0.5** | **Pure-XAML PoC** | Add Dock.Avalonia.Themes.Simple to App.axaml. Add "Dock spike" tab with hand-written XAML DockControl + 2 static tool panels. **No C# code.** | Spike tab shows tool panels with placeholder text. **If empty, STOP.** | `src/App.axaml` |
| **M1** | **Factory + Models** | Create `src/Docking/` with model classes, `AeroDockFactory`, tool/document wrappers. Factory-driven layout in spike tab. | Same visual as M0.5, driven by factory. Layout tree logged. | `src/Docking/Model/*.cs`, `AeroDockFactory.cs`, tool wrappers |
| **M2** | **Wire Real VMs** | `WireViewModels()` injects Context for all 5 panels. Still inside spike tab. | Spike shows real file tree, Git, problems, output, editor. File open works. | `MainWindow.axaml.cs` — `WireViewModels()` |
| **M3** | **Promote to Window** | Add `LayoutMode` (default: Grid). Freeform mode hides Grid, shows full DockControl. Both modes share ShellViewModel. | Switch View → Freeform → full dock layout visible. Switch back → Grid unchanged. | `ShellViewModel.cs` — `LayoutMode` |
| **M4** | **Toggle Parity** | Map toggle commands to DockControl via `SyncDockVisibility()`. All keybindings preserved. | View menu toggles work in Freeform. Ctrl+` toggles Output. 545 tests pass. | `ShellViewModel.cs` — toggle methods |
| **M5** | **Persistence** | `LayoutPersistenceService` via `Dock.Serializer.SystemTextJson`. Save/restore layout. | Close → reopen → layout preserved. Corrupt file → fallback. | `LayoutPersistenceService.cs` |
| **M6** | **Default Flip** | Change default LayoutMode to Freeform. Update PHASES.md. Add manual test script. | Build 0 errors. Tests all pass. Manual smoke complete. | `ShellViewModel.cs` — default change |

---

## Recommended DataTemplate Strategy: Option A (Unanimous)

```xml
<!-- MainWindow.axaml — DataTemplates with NO DataContext binding -->
<Window.DataTemplates>
    <DataTemplate DataType="dockTools:ExplorerTool">   <views:FileExplorerView/></DataTemplate>
    <DataTemplate DataType="dockTools:GitTool">        <views:GitPanelView/></DataTemplate>
    <DataTemplate DataType="dockTools:ProblemsTool">   <views:ProblemsView/></DataTemplate>
    <DataTemplate DataType="dockTools:OutputTool">     <views:OutputView/></DataTemplate>
    <DataTemplate DataType="dockDocs:EditorDocument">  <views:EditorView/></DataTemplate>
</Window.DataTemplates>
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


## Initialization Sequence

### App.axaml — Theme Include (CRITICAL)

> **Note:** The exact StyleInclude URI must be verified by inspecting the installed `Dock.Avalonia.Themes.Simple` package contents. Do NOT guess the path (this was suspected as a v1 root cause).

```xml
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
    <!-- VERIFY THE EXACT URI against installed package before committing -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/SimpleDockTheme.xaml" />
    <StyleInclude Source="avares://aero/Styles/ControlThemes.axaml" />
</Application.Styles>
```

### App.axaml.cs — OnFrameworkInitializationCompleted()

```csharp
var shell = _services.GetRequiredService<ShellViewModel>();
var bus = _services.GetRequiredService<IMessageBus>();
_services.GetRequiredService<LSPManager>();
_services.GetRequiredService<GitViewModel>();

// 1. Create window with DataContext (BEFORE Initialize)
var mainWindow = new MainWindow { DataContext = shell };
Log("[Dock] DataContext set on MainWindow");

// 2. Initialize (bus subscriptions + optional dock setup)
mainWindow.Initialize(bus);

desktop.MainWindow = mainWindow;
```

### MainWindow.axaml.cs — Initialize() + DockControl

```csharp
public void Initialize(IMessageBus bus)
{
    if (bus == null) throw new ArgumentNullException(nameof(bus));
    if (DataContext is not ShellViewModel shell)
        throw new InvalidOperationException("DataContext not set before Initialize");

    _bus = bus;
    // A. Bus subscriptions (unchanged)
    _bus.Subscribe(_confirmDirtyCloseHandler = OnConfirmDirtyClose);
    // ... other subs ...

    // B. DockControl setup (only in Freeform mode, from M1+)
    if (shell.LayoutMode == LayoutMode.Freeform)
        InitializeDockControl(shell);
}

private void InitializeDockControl(ShellViewModel shell)
{
    Log("[Dock] init: begin");
    var factory = new AeroDockFactory();
    var layout = factory.CreateDefaultLayout();
    Log($"[Dock] init: {CountDockables(layout)} dockables");

    WireViewModels(layout, shell);              // Context injection BEFORE layout
    DockControl.InitializeFactory = true;
    DockControl.InitializeLayout = false;
    DockControl.Factory = factory;
    DockControl.Layout = layout;                 // LAST — triggers rendering
    Log("[Dock] init: layout assigned");
}
```

---

## Layout Tree Structure

```
AeroRootDock
└── AeroProportionalDock (Orientation.Horizontal)
    ├── AeroProportionalDock (Vertical, Proportion=0.22)
    │   └── AeroToolDock (Alignment.Left)
    │       ├── ExplorerTool    (Context = FileExplorerViewModel)
    │       └── GitTool         (Context = GitViewModel)
    ├── AeroProportionalDockSplitter
    └── AeroProportionalDock (Vertical, Proportion=0.78)
        ├── AeroDocumentDock (Proportion=0.72)
        │   └── EditorDocument (Context = EditorViewModel)
        ├── AeroProportionalDockSplitter
        └── AeroToolDock (Alignment.Bottom, Proportion=0.28)
            ├── ProblemsTool    (Context = ProblemsViewModel)
            └── OutputTool      (Context = OutputViewModel)
```

**Proportion constants:** Left=0.22, Right=0.78, Editor=0.72, Bottom=0.28


## Hide/Show Semantics (M4)

The four `Reactive` booleans on `ShellViewModel` remain the source of truth:

| Action | Grid Mode (unchanged) | Freeform Mode (M4) |
|--------|----------------------|-------------------|
| Hide sidebar | `IsSidebarVisible = false` | Remove left `IToolDock` from parent `VisibleDockables`; remember index |
| Show sidebar | `IsSidebarVisible = true` | Re-insert at remembered index, restore Proportion |
| Switch sidebar tab | `ActiveSidebarTabIndex = 1` | `leftToolDock.ActiveDockable = leftToolDock.VisibleDockables[1]` |
| Toggle Output | `IsBottomPanelVisible = true; ActiveBottomTabIndex = 1` | Ensure bottom `IToolDock` is in `VisibleDockables`; set `ActiveDockable = outputTool` |
| Toggle Problems | `IsBottomPanelVisible = true; ActiveBottomTabIndex = 0` | Same pattern, target ProblemsTool |

---

## Logging Strategy (From M1)

Channel: `Debug.WriteLine` with `[Dock]` prefix + `StatusMessage` for Info level.

| Site | Level | Message |
|------|-------|---------|
| `AeroDockFactory.CreateDefaultLayout()` | Debug | Tree dump (type, id, proportion per child) |
| `InitializeDockControl()` | Info | `[Dock] init: begin / factory assigned / layout built / ViewModels wired / layout assigned` |
| `WireViewModels()` | Debug | `[Dock] Wired {Type}.Context -> {ContextType}` per dockable |
| `OnClosing()` (M5) | Debug | `[Dock] Persisting layout to {path}` |
| `LayoutPersistenceService.Load()` (M5) | Info | `[Dock] Layout loaded (N bytes)` or fallback reason |

---

## Scope Boundaries (Not Doing)

| Out of Scope | Reason | Future Phase |
|-------------|--------|-------------|
| **Tile Mode (8.1b)** | Different layout engine entirely | 8.1b |
| **Tear-Away Windows (8.1c)** | Requires OS window management | 8.1c |
| **IDockingService abstraction** | Premature — only one concrete impl | 8.1b |
| **Option B {Binding Context}** | Unverified, adds risk | Future cleanup |
| **Remove ShellViewModel booleans** | Used by tests, persistence, Grid fallback | Keep forever |
| **DialogHost.Avalonia** | Incompatible with Avalonia 11.3 | Avalonia 12 |

---

## Files to Create

| File | Milestone | Purpose |
|------|-----------|---------|
| `src/Docking/LayoutMode.cs` | M1 | `enum LayoutMode { Grid, Freeform }` |
| `src/Docking/AeroDockFactory.cs` | M1 | `IFactory` implementation |
| `src/Docking/Model/AeroRootDock.cs` | M1 | `IRootDock` impl |
| `src/Docking/Model/AeroToolDock.cs` | M1 | `IToolDock` impl |
| `src/Docking/Model/AeroDocumentDock.cs` | M1 | `IDocumentDock` impl |
| `src/Docking/Model/AeroProportionalDock.cs` | M1 | `IProportionalDock` impl |
| `src/Docking/Model/AeroProportionalDockSplitter.cs` | M1 | Splitter impl |
| `src/Docking/ToolViewModels/ExplorerTool.cs` | M1 | Explorer `ITool` wrapper |
| `src/Docking/ToolViewModels/GitTool.cs` | M1 | Git `ITool` wrapper |
| `src/Docking/ToolViewModels/ProblemsTool.cs` | M1 | Problems `ITool` wrapper |
| `src/Docking/ToolViewModels/OutputTool.cs` | M1 | Output `ITool` wrapper |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | M1 | Editor `IDocument` wrapper |
| `src/Services/LayoutPersistenceService.cs` | M5 | Layout save/restore |

## Files to Modify

| File | Milestone | Changes |
|------|-----------|---------|
| `src/App.axaml` | M0.5 | Add Dock.Avalonia.Themes.Simple StyleInclude |
| `src/MainWindow.axaml` | M0.5 | Add "Dock spike" tab for PoC |
| | M1 | Replace spike with `<DockControl>` + DataTemplates |
| `src/MainWindow.axaml.cs` | M1 | Add `InitializeDockControl()`, `WireViewModels()`, `SyncDockVisibility()` |
| | M3 | Respect `LayoutMode` — skip Dock init when Grid |
| `src/App.axaml.cs` | M1 | Ensure Initialize() called after DataContext set (already correct) |
| `src/ViewModels/ShellViewModel.cs` | M3 | Add `LayoutMode` property, mode-switch command |
| | M4 | Rewrite toggles to call `SyncDockVisibility()` |
| | M6 | Change default to `Freeform` |
| `src/Services/SettingsService.cs` | M5 | Add layout persistence to workspace state |

## Files NOT to Modify

- `src/ViewModels/EditorViewModel.cs` — Tab management unchanged
- `src/ViewModels/FileExplorerViewModel.cs` — File tree logic unchanged
- `src/ViewModels/GitViewModel.cs` — Git operations unchanged
- `src/ViewModels/ProblemsViewModel.cs` — Diagnostic display unchanged
- `src/ViewModels/OutputViewModel.cs` — Build output unchanged
- `src/Services/DocumentManager.cs` — Document lifecycle unchanged
- `src/Views/*.axaml` — All View files unchanged (DataContext from Context injection)

---

## Agent Contributions

| Agent | Key Contribution |
|-------|-----------------|
| **Minimax** | Simple milestone breakdown, proportion 0.25/0.75 |
| **Deepseek** | Most detailed file lists, risk likelihood/impact scoring |
| **Claude** | `SyncDockVisibility()` pattern, files NOT to modify |
| **Hy3** | Simple logging (Console.WriteLine with `[Dock]` prefix) |
| **Blackbox** | Refined proportions 0.22/0.78, observability-first M0.5 |
| **Opus** | **LayoutMode switch** — the architectural breakthrough, scope boundaries |
| **Qwen** | Confirmed consensus on all major decisions |

---

## Rollback Strategy

```bash
# Tag each milestone
git tag v2-m0          # Baseline
git tag v2-m0.5        # PoC
git tag v2-m1          # Factory + models
git tag v2-m2          # ViewModels wired
git tag v2-m3          # LayoutMode switch
git tag v2-m4          # Toggle parity
git tag v2-m5          # Persistence
git tag v2-m6          # Default flip

# Rollback to any point:
git reset --hard v2-m{N}
```
