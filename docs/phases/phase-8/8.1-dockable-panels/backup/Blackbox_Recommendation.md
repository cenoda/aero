# Blackbox_Recommendation.md

## Approach Overview
Use a **proof-first, incremental docking migration** that preserves all existing Phase 0–7 behaviors while replacing the current Grid layout with Dock.Avalonia Freeform layout in controlled steps. The plan starts with a minimal DockControl rendering PoC (single tool with static content) to validate theme + sizing + initialization assumptions before wiring real ViewModels. Then progressively add tool panels, document panel, command/toggle bridges, and persistence, with explicit logging and a rollback commit after each milestone. This directly addresses the previous failure mode (late integration, uncertain internals, weak observability) and keeps each step independently testable.

## Recommended DataTemplate Strategy
**Option A — Direct Context injection (`IDockable.Context`)**.

### Why Option A
1. **Most resilient to DeferredContentControl parent-chain breakage**  
   Post-mortem and analysis both show `$parent[Window].DataContext` is unreliable inside Dock's deferred visual tree.
2. **Already partially validated in failed attempt logs**  
   Context injection produced correct wiring diagnostics for all tools/documents even when rendering still failed for other reasons.
3. **Deterministic timing**  
   We explicitly assign `Context` in code after `ShellViewModel` exists and before `DockControl.Layout` assignment, avoiding ambiguous DataTemplate timing.
4. **Debuggability**  
   We can log each `dockable.Id -> context type` mapping at wire time.

### DataTemplate shape
Keep templates passive (no parent binding hacks):
```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
</DataTemplate>
```
Set in code:
```csharp
explorerTool.Context = shell.FileExplorerViewModel;
```

## Milestone Plan

| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|-------------------|----------------|
| **M0.5** | Baseline verification + observability scaffolding. Add dedicated docking logger category and startup trace points in `App.axaml.cs` and `MainWindow.axaml.cs`. | `dotnet build`, `dotnet test`, run app and confirm startup logs appear in expected order (DataContext set, Initialize called). | `commit: phase8.1a-m0.5-observability` |
| **M1 (PoC-first)** | Minimal Dock.Avalonia proof in app: include Dock theme in `App.axaml`, render `DockControl` with one static tool dockable + obvious content text. No real panel VM wiring yet. | App visually shows tool panel content and non-zero sized dock host. If fails, stop and fix before proceeding. | `commit: phase8.1a-m1-dock-poc` |
| **M2** | Introduce docking infrastructure (`src/Docking/...`) with minimal factory and explicit `Proportion` values. Still use placeholder content for left/center/bottom regions to validate layout geometry. | Left/center/bottom all visible, splitters draggable, proportions respected after resize. | `commit: phase8.1a-m2-layout-geometry` |
| **M3** | Wire real tool ViewModels via **Option A Context injection** (Explorer, Git, Problems, Output) and document/editor dockable. Keep Grid removed, DockControl primary host. | Explorer/Git tabs visible; Problems/Output tabs visible; editor area populated; open folder updates explorer and git in docked UI. | `commit: phase8.1a-m3-real-vm-wiring` |
| **M4** | Bridge existing Shell toggles/commands to dock model (`ToggleSidebar`, `ToggleBottomPanel`, tab switching commands). Preserve existing keyboard/menu command behavior. | Existing shortcuts/menu actions still work; toggle commands show/hide expected dockables or activate expected tabs. | `commit: phase8.1a-m4-command-parity` |
| **M5** | Initialization hardening and lifecycle: enforce sequence (`DataContext` before dock init), move all dock setup into `Initialize(...)` path, add null/ordering guards and verbose diagnostics. | Cold start + workspace restore path both work; no null-context logs; no constructor-time dock init. | `commit: phase8.1a-m5-init-hardening` |
| **M6** | Layout persistence (Freeform only): save/load Dock layout + panel visibility state, safe fallback to default layout on deserialize failure; add mode-switch placeholder (Tile stub only). | Restart restores layout; corrupted layout file falls back cleanly with warning log; Tile option present as stub/no-op note. | `commit: phase8.1a-m6-persistence-freeform` |

## Key Risks & Mitigations

1. **Risk: Dock theme missing or loaded too late (zero-size / invisible panels)**  
   - Mitigation: Add Dock.Avalonia theme include in `App.axaml` at M1.  
   - Add startup log confirming style loaded and DockControl theme resources resolved.

2. **Risk: Initialization race (`InitializeDockControl` before DataContext)**  
   - Mitigation: forbid constructor-time dock setup.  
   - Gate dock initialization in `MainWindow.Initialize(IMessageBus bus, ShellViewModel shell)` or equivalent post-DataContext path.  
   - Log order checkpoints.

3. **Risk: Context not bound at template render time**  
   - Mitigation: Option A direct context injection before assigning `DockControl.Layout`.  
   - Emit per-dockable wire logs (`DockableId`, `ContextType`, null check).

4. **Risk: Layout tree valid in memory but visually collapsed**  
   - Mitigation: explicit `Proportion` on every `ProportionalDock` child (e.g., 0.22 / 0.78 and center/bottom 0.72 / 0.28).  
   - Add geometry logs after layout creation.

5. **Risk: Existing commands/shortcuts regress**  
   - Mitigation: M4 dedicated parity milestone; no persistence or extra refactor during parity work.  
   - Manual verification matrix for Ctrl+` , Ctrl+Shift+B, view toggles, menu commands.

6. **Risk: Persistence introduces startup failures**  
   - Mitigation: safe deserialize wrapper with fallback default layout and versioned schema marker.  
   - Save on controlled points (window close / significant layout change debounce), not every mutation.

## Initialization Sequence (Pseudo-Code)

### App.axaml (styles)
```xml
<Application.Styles>
  <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml"/>
  <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/Default.xaml"/>
  <!-- existing app styles -->
</Application.Styles>
```

### App.axaml.cs (construction order)
```csharp
public override void OnFrameworkInitializationCompleted()
{
    _services = BuildServices();

    var shell = _services.GetRequiredService<ShellViewModel>();
    var bus = _services.GetRequiredService<IMessageBus>();

    var window = new MainWindow();

    // 1) Set DataContext FIRST
    window.DataContext = shell;
    Log("MainWindow.DataContext assigned", shell.GetType().Name);

    // 2) Call Initialize (bus subscriptions + dock init entrypoint)
    window.Initialize(bus /*, shell if signature expanded */);
    Log("MainWindow.Initialize completed");

    desktop.MainWindow = window;

    // 3) Continue with workspace restore / CLI folder open
}
```

### MainWindow.axaml.cs (dock setup order)
```csharp
public void Initialize(IMessageBus bus)
{
    // A) existing bus subscriptions
    SubscribeBus(bus);

    // B) resolve shell from DataContext (must be non-null now)
    var shell = DataContext as ShellViewModel
        ?? throw new InvalidOperationException("ShellViewModel missing before dock init");

    // C) create default layout model (with explicit proportions)
    var layout = _dockFactory.CreateDefaultLayout();
    LogLayout(layout);

    // D) wire real VM contexts into dockables (Option A)
    WireViewModels(layout, shell);   // explorer.Context = shell.FileExplorerViewModel etc.

    // E) assign factory and layout LAST
    DockControl.InitializeFactory = true;
    DockControl.InitializeLayout = false;
    DockControl.Factory = layout.Factory!;
    DockControl.Layout = layout;

    Log("DockControl layout assigned");
}
```

### Proportion defaults (explicit)
- Root horizontal: **Left 0.22 / Right 0.78**
- Right vertical: **Editor 0.72 / Bottom 0.28**
- Tool/document docks with multiple tabs must set an initial `ActiveDockable`.

## Additional implementation notes
- Preserve current `ShellViewModel` public command surface so keyboard shortcuts and menus in `MainWindow.axaml` remain unchanged.
- During transition, avoid mixing old Grid visibility booleans directly with visual tree assumptions; map booleans to dockable visibility/active state in one adapter layer.
- Keep Freeform mode as active mode; define a future enum/stub (e.g., `LayoutMode.Tile`) without implementation side effects in this phase.
