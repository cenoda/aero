# Copilot_Recommendation.md

> **Author:** GitHub Copilot
> **Date:** 2026-06-23
> **Branch:** `phase-8.1a-dockable-panels-v2`
> **Predecessor:** `failed-dockable-panels` (reverted — see POSTMORTEM-phase-8.1a.md)

---

## Approach Overview

This plan replaces the current hard-coded Grid layout with Dock.Avalonia's `DockControl` in six incremental milestones, each independently testable and revertible. The critical lesson from the failed first attempt is: **validate the library with a minimal spike before writing any real code**. This plan opens with M0.5 — a proof-of-concept that creates a single `DockControl` rendering a dummy panel — to confirm Dock.Avalonia's initialization sequence, DataTemplate resolution, and theme wiring work correctly in our Avalonia 11.3 environment. Only after M0.5 passes do we proceed to replace the real layout.

The strategy is: **Option A (Direct Context Injection)** for DataTemplates, a strict initialization order of `DataContext → Initialize() → Factory → Layout`, explicit `Proportion` values on every `IProportionalDock` child, `Dock.Avalonia.Themes.Simple` style in `App.axaml`, and logging from the very first milestone.

---

## Recommended DataTemplate Strategy

### Selected: Option A — Direct Context Injection

**Why Option A over Option B:**

| Factor | Option A (Context Injection) | Option B (Binding) |
|--------|------------------------------|---------------------|
| Verified in v1 | ✅ Context was set correctly on all 5 tools | ❌ Never tested independently |
| Timing risk | None — injection happens in code after DataContext is set | High — Dock.Avalonia's `DeferredContentControl` may not guarantee Context → DataContext chain |
| MVVM purity | Moderate — code-behind sets Context | High — pure binding, no code-behind |
| Debuggability | Easy — breakpoint on injection | Hard — binding failure is silent |
| `$parent[Window]` issue | Not applicable | Risk of same failure as v1 |

**Decision:** The v1 post-mortem confirmed that `$parent[Window].DataContext` breaks inside Dock.Avalonia's `DeferredContentControl`. Option B's `{Binding Context}` is safer than `$parent[Window]` but still unproven. Option A was verified working in v1 (all 5 Contexts set correctly, folder open worked). The MVVM trade-off is acceptable for a critical integration point where debuggability matters more than purity.

**Implementation pattern:**

```xml
<!-- MainWindow.axaml — No DataContext binding, just the View -->
<DataTemplate DataType="{x:Type local:ExplorerTool}">
    <views:FileExplorerView/>
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — WireViewModels() called after DataContext is set
private void WireViewModels(AeroDockFactory factory, ShellViewModel shell)
{
    foreach (var dockable in factory.GetDockables())
    {
        switch (dockable)
        {
            case ExplorerTool t:
                t.Context = shell.FileExplorerViewModel;
                t.Title = "Explorer";
                Log("WireViewModels: ExplorerTool.Context = FileExplorerViewModel");
                break;
            case GitTool t:
                t.Context = shell.GitViewModel;
                t.Title = "Git";
                Log("WireViewModels: GitTool.Context = GitViewModel");
                break;
            case ProblemsTool t:
                t.Context = shell.ProblemsViewModel;
                t.Title = "Problems";
                Log("WireViewModels: ProblemsTool.Context = ProblemsViewModel");
                break;
            case OutputTool t:
                t.Context = shell.OutputViewModel;
                t.Title = "Output";
                Log("WireViewModels: OutputTool.Context = OutputViewModel");
                break;
            case EditorDocument d:
                d.Context = shell.EditorViewModel;
                d.Title = "Editor";
                Log("WireViewModels: EditorDocument.Context = EditorViewModel");
                break;
        }
    }
}
```

---

## Milestone Plan

| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|-------------------|----------------|
| **M0.5** | **Proof-of-concept spike**: Create minimal DockControl with 1 dummy panel, verify rendering, theme, init sequence | Dummy panel renders in window; logs confirm Factory → Layout → Render chain | No existing code changed — spike is additive only |
| **M1** | **Dock infrastructure skeleton**: Create `AeroDockFactory`, model classes (`AeroRootDock`, `AeroToolDock`, `AeroDocumentDock`, `AeroProportionalDock`, `AeroProportionalDockSplitter`), tool/document wrappers (`ExplorerTool`, `GitTool`, `ProblemsTool`, `OutputTool`, `EditorDocument`). Replace Grid in `MainWindow.axaml` with `DockControl`. Wire in `MainWindow.axaml.cs`. | Build passes. App launches. DockControl visible (even if empty). All 5 DataTemplates resolve (logs confirm). | `git revert` M0.5 spike commit |
| **M2** | **Panel wiring**: Set Context on all 5 dockables. Verify each panel renders with real content. Test Explorer file tree, Git status, Problems list, Output log, Editor tabs. | All 5 panels visible with real content. Explorer can expand folders. Git shows branch/status. Editor shows open file tabs. | `git revert` M2 commit |
| **M3** | **Layout tree + proportions**: Build the full layout tree (left column: Explorer+Git, center: Editor, bottom: Problems+Output) with explicit `Proportion` values. Add `IProportionalDockSplitter` between zones. | Layout matches current Grid behavior: sidebar left, editor center, bottom panel below editor. Splitters draggable. Proportions visually correct (sidebar ~25%, editor ~75%). | `git revert` M3 commit |
| **M4** | **Toggle commands**: Rewrite `ShellViewModel` toggle commands (`ToggleSidebarCommand`, `ToggleBottomPanelCommand`, etc.) to walk the dock tree and toggle `IsVisible` on the relevant `IToolDock`. Preserve all existing keyboard shortcuts. | All 5 View > Toggle menu items work. Ctrl+OemTilde toggles Output. Keyboard shortcuts unchanged. Panels hide/show without crash. | `git revert` M4 commit |
| **M5** | **Layout persistence**: Save/restore layout to `~/.aero/layout.json` via `Dock.Serializer.SystemTextJson`. Persist on window close, restore on launch. | Close app with rearranged panels → reopen → layout preserved. First launch creates default layout. Corrupt JSON → falls back to default. | `git revert` M5 commit |
| **M6** | **Cleanup + docs**: Update PHASES.md, add manual test script, remove dead code, add XML doc comments, final review. | `dotnet build` 0 errors. `dotnet test` all pass. `manual_test_phase8_1a.sh` validates all panels. | `git revert` M6 commit |

---

## Key Risks & Mitigations

### Risk 1: Dock.Avalonia Internal Rendering (HIGH — this is what killed v1)

**What could go wrong:** Panels exist in the layout tree but don't render, or only one panel fills the window (exactly what happened in v1).

**Mitigations:**
- **M0.5 spike first** — if the spike fails, we stop before writing any real code
- **Logging at every step** — Factory.Create*, WireViewModels, Layout assignment all logged
- **Incremental milestones** — if M2 (panel wiring) works but M3 (layout tree) fails, we know the problem is in the tree structure, not the DataTemplates
- **Known v1 issues addressed:**
  - `Dock.Avalonia.Themes.Simple` WILL be added to `App.axaml` (suspected v1 root cause)
  - `InitializeDockControl()` WILL be called in `MainWindow.Initialize()`, NOT in the constructor (v1 timing bug)
  - Explicit `Proportion` values on ALL `IProportionalDock` children (v1 had missing proportions)
  - No `$parent[Window].DataContext` bindings (v1 binding failure)

### Risk 2: IFactory / Concrete Types (MEDIUM)

**What could go wrong:** `IFactory` methods return interfaces; we may not have access to concrete types from Dock.Avalonia.

**Mitigation:**
- M0.5 spike tests exactly this: which concrete types are public?
- Fallback: extend `FactoryBase` if available; implement `IFactory` from scratch if not
- The v1 `AeroDockFactory` compiled and worked — the same types should be available

### Risk 3: Dock.Avalonia Version Compatibility (MEDIUM)

**What could go wrong:** Dock.Avalonia 11.3.* has breaking changes or undocumented behavior differences from examples found online.

**Mitigation:**
- All packages pinned to 11.3.* (already in `aero.csproj`)
- M0.5 spike validates against our specific Avalonia 11.3 + .NET 9 combination
- No use of proposed or unstable APIs

### Risk 4: ShellViewModel Rewrite Breaking Existing Behavior (LOW)

**What could go wrong:** Rewriting toggle commands to walk dock tree breaks keyboard shortcuts or menu items.

**Mitigations:**
- M4 is isolated — toggle commands are the ONLY change in that milestone
- All keyboard shortcuts are defined in `MainWindow.axaml` KeyBindings and bind to ShellViewModel commands — the command names don't change, only their internal implementation
- Manual test script validates every shortcut

### Risk 5: Layout Persistence Schema Drift (LOW)

**What could go wrong:** Dock.Serializer schema changes between versions, breaking saved layouts.

**Mitigations:**
- Layout file is optional — if missing or corrupt, fall back to default layout
- Use `Dock.Serializer.SystemTextJson` (already installed) for stable serialization
- Include a version field in the layout JSON

---

## Initialization Sequence (Pseudo-Code)

The initialization order is critical. The v1 failure was partly caused by calling `InitializeDockControl()` in the constructor before `DataContext` was set. Here is the correct sequence:

### App.axaml.cs — OnFrameworkInitializationCompleted()

```csharp
public override void OnFrameworkInitializationCompleted()
{
    _services = BuildServices();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Theme (unchanged)
        var themeService = _services.GetRequiredService<ThemeService>();
        themeService.WireThemeDictionaries();
        _ = themeService.ApplyThemeAsync();

        var shell = _services.GetRequiredService<ShellViewModel>();
        var bus = _services.GetRequiredService<IMessageBus>();

        // Eagerly resolve managers (unchanged)
        _services.GetRequiredService<LSPManager>();
        _services.GetRequiredService<GitViewModel>();

        // STEP 1: Create MainWindow with DataContext (CRITICAL: DataContext set BEFORE Initialize)
        var mainWindow = new MainWindow { DataContext = shell };

        // STEP 2: Initialize bus subscriptions (DataContext is now set)
        mainWindow.Initialize(bus);

        // STEP 3: Initialize dock (after DataContext, after bus)
        mainWindow.InitializeDockControl();

        desktop.MainWindow = mainWindow;

        // CLI args / workspace restore (unchanged)
        if (desktop.Args is { Length: > 0 } args && Directory.Exists(args[0]))
        {
            bus.Publish(new FolderOpened(Path.GetFullPath(args[0])));
        }
        else
        {
            _ = RestoreWorkspaceAsync(shell, mainWindow, bus);
        }

        desktop.Exit += OnDesktopExit;
    }

    base.OnFrameworkInitializationCompleted();
}
```

### MainWindow.InitializeDockControl()

```csharp
/// <summary>
/// Initialize the Dock.Avalonia layout. Must be called AFTER DataContext is set
/// and AFTER Initialize(bus) so that ShellViewModel is accessible.
/// </summary>
public void InitializeDockControl()
{
    if (DataContext is not ShellViewModel shell)
    {
        throw new InvalidOperationException(
            "InitializeDockControl called before DataContext is set");
    }

    Log("InitializeDockControl: Starting dock initialization");

    // STEP A: Create factory
    _dockFactory = new AeroDockFactory();
    Log($"InitializeDockControl: Factory created — {_dockFactory.GetDockables().Count()} dockables");

    // STEP B: Create default layout
    var layout = _dockFactory.CreateDefaultLayout();
    Log($"InitializeDockControl: Default layout created — root type: {layout.GetType().Name}");

    // STEP C: Wire ViewModels into dockables (Context injection)
    WireViewModels(_dockFactory, shell);
    Log("InitializeDockControl: ViewModels wired");

    // STEP D: Set layout on DockControl (LAST STEP — triggers rendering)
    DockControl.Layout = layout;
    Log($"InitializeDockControl: Layout assigned — DockControl rendering should start");
}
```

### MainWindow.WireViewModels()

```csharp
private void WireViewModels(AeroDockFactory factory, ShellViewModel shell)
{
    foreach (var dockable in factory.GetDockables())
    {
        switch (dockable)
        {
            case ExplorerTool t:
                t.Context = shell.FileExplorerViewModel;
                t.Title = "Explorer";
                break;
            case GitTool t:
                t.Context = shell.GitViewModel;
                t.Title = "Git";
                break;
            case ProblemsTool t:
                t.Context = shell.ProblemsViewModel;
                t.Title = "Problems";
                break;
            case OutputTool t:
                t.Context = shell.OutputViewModel;
                t.Title = "Output";
                break;
            case EditorDocument d:
                d.Context = shell.EditorViewModel;
                d.Title = "Editor";
                break;
        }

        Log($"WireViewModels: {dockable.GetType().Name} ({dockable.Id}) — Context set");
    }
}
```

### App.axaml — Theme Include (CRITICAL — missing in v1)

```xml
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
    <!-- Dock.Avalonia theme — WITHOUT this, DockControl renders with zero size -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/Themes/SimpleTheme.axaml" />
    <!-- Phase 8.9 Control Themes — layered after SimpleTheme so overrides win -->
    <StyleInclude Source="avares://aero/Styles/ControlThemes.axaml" />
</Application.Styles>
```

---

## Logging Strategy

Logging from the very first milestone (per AGENTS.md §9 Lesson #4):

| Location | What to Log | Level |
|----------|-------------|-------|
| `AeroDockFactory.CreateDefaultLayout()` | Layout tree structure (parent → children, proportions, dockable IDs) | Debug |
| `AeroDockFactory.CreateTool()` / `CreateDocument()` | Each dockable created (type, Id) | Debug |
| `MainWindow.InitializeDockControl()` | Factory created, layout created, ViewModels wired, layout assigned | Info |
| `MainWindow.WireViewModels()` | Each Context injection (type, Id, Context type) | Debug |
| `DockControl.Layout` setter | Layout assigned — rendering should start | Info |
| `MainWindow.OnClosing()` | Layout persistence save attempt | Debug |

All logging via `System.Diagnostics.Debug.WriteLine()` for debug builds, with a consistent prefix `[Dock]` for easy filtering.

---

## Layout Tree Structure (M3)

```
AeroRootDock (layout root)
└── AeroProportionalDock (Orientation.Horizontal)
    ├── AeroProportionalDock (Orientation.Vertical, Proportion=0.25)
    │   └── AeroToolDock (Alignment.Left)
    │       ├── ExplorerTool (Context=FileExplorerViewModel)
    │       └── GitTool (Context=GitViewModel)
    ├── AeroProportionalDockSplitter
    └── AeroProportionalDock (Orientation.Vertical, Proportion=0.75)
        ├── AeroDocumentDock (Proportion=0.7)
        │   └── EditorDocument (Context=EditorViewModel)
        ├── AeroProportionalDockSplitter
        └── AeroToolDock (Alignment.Bottom, Proportion=0.3)
            ├── ProblemsTool (Context=ProblemsViewModel)
            └── OutputTool (Context=OutputViewModel)
```

---

## Files to Create

| File | Milestone | Purpose |
|------|-----------|---------|
| `src/Docking/LayoutMode.cs` | M1 | `enum LayoutMode { Freeform, Tile }` |
| `src/Docking/AeroDockFactory.cs` | M0.5/M1 | `IFactory` implementation — creates layout tree, registers DataTemplates |
| `src/Docking/Model/AeroRootDock.cs` | M1 | Concrete `IRootDock` implementation |
| `src/Docking/Model/AeroToolDock.cs` | M1 | Concrete `IToolDock` implementation |
| `src/Docking/Model/AeroDocumentDock.cs` | M1 | Concrete `IDocumentDock` implementation |
| `src/Docking/Model/AeroProportionalDock.cs` | M1 | Concrete `IProportionalDock` implementation |
| `src/Docking/Model/AeroProportionalDockSplitter.cs` | M1 | Concrete `IProportionalDockSplitter` implementation |
| `src/Docking/ToolViewModels/ExplorerTool.cs` | M1 | `ITool` for File Explorer panel |
| `src/Docking/ToolViewModels/GitTool.cs` | M1 | `ITool` for Git panel |
| `src/Docking/ToolViewModels/ProblemsTool.cs` | M1 | `ITool` for Problems panel |
| `src/Docking/ToolViewModels/OutputTool.cs` | M1 | `ITool` for Output panel |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | M1 | `IDocument` for Editor |
| `src/Docking/LayoutPersistenceService.cs` | M5 | Save/restore layout JSON |

## Files to Modify

| File | Milestone | Changes |
|------|-----------|---------|
| `src/App.axaml` | M0.5 | Add `<StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/..."/>` |
| `src/MainWindow.axaml` | M1 | Replace Grid with `<DockControl x:Name="DockControl"/>` + DataTemplates |
| `src/MainWindow.axaml.cs` | M1 | Add `InitializeDockControl()`, `WireViewModels()`, logging |
| `src/App.axaml.cs` | M1 | Call `mainWindow.InitializeDockControl()` after `Initialize(bus)` |
| `src/ViewModels/ShellViewModel.cs` | M4 | Rewrite toggle commands to walk dock tree |
| `src/Services/SettingsService.cs` | M5 | Add layout persistence to workspace state |

---

*This plan addresses every root cause identified in the Phase 8.1a post-mortem: missing theme, initialization timing, binding failures, lack of incremental testing, and insufficient logging.*
