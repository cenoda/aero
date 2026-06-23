# Claude_Recommendation.md

## Approach Overview

This plan takes an **ultra-incremental, proof-of-concept-first** approach to implementing Dock.Avalonia panels. Each milestone is independently testable and committed as a separate git checkpoint. The core strategy is: (1) prove Dock.Avalonia renders a single tool panel in isolation before wiring anything else, (2) add panels one at a time, (3) preserve the existing ShellViewModel toggle commands by mapping them to DockControl operations, (4) add layout persistence last. Logging is added from the very first line of Dock.Avalonia code so any rendering issues can be traced immediately.

---

## Recommended DataTemplate Strategy

**Option A — Direct Context Injection (chosen)**

```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
    <!-- No DataContext binding — Context injected from code-behind -->
</DataTemplate>
```

```csharp
explorerTool.Context = shell.FileExplorerViewModel;
```

**Justification:**
1. **Verified working** — In the failed attempt, logs confirmed Context was correctly injected on all 5 tools. The only remaining issue was layout rendering (theme + proportions), not data binding.
2. **Avoids DeferredContentControl timing trap** — `$parent[Window].DataContext` breaks because Dock.Avalonia rebuilds the visual tree inside `DeferredContentControl`. Option A bypasses this entirely.
3. **Deterministic timing** — Context is set explicitly in code-behind after DataContext is assigned, eliminating uncertainty about when bindings resolve.
4. **Minimal XAML** — DataTemplates are clean: just the View type, no binding expressions to debug.

**Rejected Option B** (`{Binding Context}` in DataTemplate) because Dock.Avalonia's `DeferredContentControl` timing for setting Context as DataContext is not documented. Option B would introduce the same kind of non-deterministic rendering issue that killed the first attempt.

---

## Milestone Plan

| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|-------------------|----------------|
| **M0.5** | **Proof-of-concept test** — Create `Docking/TestDockFixture.cs` that programmatically creates a DockControl with 1 tool panel and renders it in a standalone Window. Add `Dock.Avalonia.Themes.Simple` StyleInclude to `App.axaml`. | Run the test fixture → see a Window with 1 tool panel (Explorer) rendered correctly. | `git tag v2-m0.5-poc` |
| **M1** | **Single tool panel (Explorer only)** — Create `Docking/Model/AeroToolDock.cs`, `AeroRootDock.cs`, and `AeroDockFactory.cs`. Replace Grid layout in `MainWindow.axaml` with `DockControl`. Add DataTemplates for ExplorerTool. Wire ViewModel via Context injection. | `dotnet run` → Explorer panel renders in left sidebar area. File tree loads and is navigable. | `git tag v2-m1-explorer` |
| **M2** | **Add Git tool panel** — Add `GitTool` model + DataTemplate. Extend `WireViewModels()` to inject `GitViewModel` into GitTool.Context. | `dotnet run` → Left sidebar shows Explorer + Git as tabs in a ToolDock. Switching tabs works. | `git tag v2-m2-git` |
| **M3** | **Add Editor document panel** — Create `EditorDocument` model + DataTemplate. Add DocumentDock to center area. Wire FileExplorerViewModel.OpenFile to create EditorDocument. Remove old `EditorView` direct placement. | `dotnet run` → Clicking a file in Explorer opens editor tab in center DocumentDock. | `git tag v2-m3-editor` |
| **M4** | **Add bottom tool panels (Problems + Output)** — Create `ProblemsTool`, `OutputTool` models + DataTemplates. Wire ViewModels. Complete the full layout: left (Explorer+Git) | splitter | center (Editor) | bottom (Problems+Output). | `dotnet run` → Full layout renders: left sidebar, editor center, bottom panel. All 5 panels show correct content. | `git tag v2-m4-full-layout` |
| **M5** | **Wire toggle commands to DockControl** — Map `IsSidebarVisible` → hide/show left ToolDock. Map `IsBottomPanelVisible` → hide/show bottom ToolDock. Map `ToggleOutput`/`ToggleProblems` → switch active tab in bottom ToolDock. Preserve all keybindings. | `dotnet run` → Ctrl+` toggles output panel. Ctrl+Shift+B shows bottom panel. View menu toggles work. Same behavior as current Grid-based layout. | `git tag v2-m5-toggles` |
| **M6** | **Layout persistence** — Add `LayoutPersistenceService` that saves/loads `DockControl.Layout` as JSON. Save on window close. Load on startup. Integrate with existing `WorkspaceState`. | Close app → reopen → layout is restored (panel positions, sizes, open tabs). | `git tag v2-m6-persistence` |

### Checkpoint Strategy

Each milestone is committed with a signed tag. Any milestone can be reverted with:
```bash
git revert <milestone-tag>..HEAD  # if last in sequence
# Or for a specific undo:
git checkout <prev-milestone-tag> -- src/Docking/ src/MainWindow.axaml src/MainWindow.axaml.cs
```

---

## Key Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Dock.Avalonia renders nothing** (same as v1) | Medium | Critical | **M0.5 proof-of-concept** validates rendering BEFORE any real code. If POC fails, stop and investigate library compatibility. |
| **Missing theme causes zero-size panels** | High | Critical | Add `Dock.Avalonia.Themes.Simple` StyleInclude to `App.axaml` in M0.5. Verify it loads before M1. |
| **ProportionalDock children get 0 proportion** | Medium | High | Every `ProportionalDock.Child` must have explicit `Proportion` value. Use constants: left=0.25, center=0.50, bottom=0.25. |
| **InitializeDockControl called before DataContext** | High | High | **Must** call `InitializeDockControl(shell)` from `App.axaml.cs` AFTER `DataContext = shell`, not from constructor. Verified pattern from failed attempt. |
| **Toggle commands break with DockControl** | Medium | High | Preserve ShellViewModel boolean properties. Map boolean changes to DockControl visibility in a dedicated method (`SyncDockVisibility()`). Don't remove booleans — they're used by unit tests and commands. |
| **DeferredContentControl breaks existing View bindings** | Low | Medium | Option A (Context injection) was verified working in v1. All 5 Views use DataContext from Context — no `$parent[Window]` bindings. |
| **Layout persistence corrupts on shutdown** | Low | Medium | Save on explicit close only (not on every resize). Validate JSON before loading. Use `DockSerializer.SystemTextJson` which is less error-prone than Newtonsoft. |
| **Editor TabControl migration fails** | Medium | High | M3 handles editor separately. Keep old `EditorView` working first, then transition to DocumentDock. The `EditorViewModel` owns `Tabs` collection — map to DocumentDock's items. |

---

## Initialization Sequence (Pseudo-Code)

### App.axaml — Add Dock.Avalonia theme

```xml
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
    <!-- NEW: Dock.Avalonia theme — REQUIRED for panels to render -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/Dock.Avalonia.Themes.Simple.xaml" />
    <StyleInclude Source="avares://aero/Styles/ControlThemes.axaml" />
</Application.Styles>
```

### App.axaml.cs — DataTemplates (for Dock.Avalonia tool/document models)

```xml
<Window xmlns:dockTools="using:Aero.Docking.Model"
        xmlns:dockDoc="using:Aero.Docking.DocumentViewModels"
        xmlns:views="using:Aero.Views">

    <Window.DataTemplates>
        <!-- Tool panels — Option A: direct View, DataContext from code-behind Context injection -->
        <DataTemplate DataType="dockTools:ExplorerTool">
            <views:FileExplorerView/>
        </DataTemplate>
        <DataTemplate DataType="dockTools:GitTool">
            <views:GitPanelView/>
        </DataTemplate>
        <DataTemplate DataType="dockTools:ProblemsTool">
            <views:ProblemsView/>
        </DataTemplate>
        <DataTemplate DataType="dockTools:OutputTool">
            <views:OutputView/>
        </DataTemplate>
        <!-- Editor documents -->
        <DataTemplate DataType="dockDoc:EditorDocument">
            <views:EditorView/>
        </DataTemplate>
    </Window.DataTemplates>

    <!-- ... rest of Window content ... -->
</Window>
```

### App.axaml.cs (OnFrameworkInitializationCompleted) — Updated initialization

```csharp
public override void OnFrameworkInitializationCompleted()
{
    _services = BuildServices();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Phase 8.2 — Theme Engine
        var themeService = _services.GetRequiredService<ThemeService>();
        themeService.WireThemeDictionaries();
        _ = themeService.ApplyThemeAsync();

        var shell = _services.GetRequiredService<ShellViewModel>();
        var bus = _services.GetRequiredService<IMessageBus>();

        // Eagerly resolve services (unchanged)
        _services.GetRequiredService<LSPManager>();
        _services.GetRequiredService<GitViewModel>();

        var mainWindow = new MainWindow();

        // STEP 1: Set DataContext BEFORE Initialize() so MainWindow can access shell
        mainWindow.DataContext = shell;

        // STEP 2: Pass bus + shell to Initialize (now shell is accessible)
        mainWindow.Initialize(bus, shell);

        desktop.MainWindow = mainWindow;

        // CLI args / workspace restore (unchanged) ...
    }
    base.OnFrameworkInitializationCompleted();
}
```

### MainWindow.axaml.cs — Initialize() with DockControl setup

```csharp
// Inject ILogger<DockingService> for debugging from M1 onwards
private readonly ILogger<DockingService>? _logger;

public void Initialize(IMessageBus bus, ShellViewModel shell)
{
    _logger = App.Services?.GetService<ILogger<DockingService>>();
    _bus = bus;

    // 1. Subscribe to bus (existing logic)
    SubscribeToBus();

    // 2. Initialize DockControl (DataContext is ALREADY set)
    InitializeDockControl(shell);
}

private void InitializeDockControl(ShellViewModel shell)
{
    // Log the start of dock initialization
    _logger?.LogInformation("Initializing DockControl...");

    // 2a. Create layout factory
    var factory = new AeroDockFactory();
    DockControl.InitializeFactory = true;
    DockControl.InitializeLayout = false;
    DockControl.Factory = factory;

    // 2b. Create default layout
    var layout = factory.CreateDefaultLayout();

    // 2c. Wire ViewModels into dockable Context properties
    WireViewModels(layout, shell);

    // 2d. Set initial visibility states from ShellViewModel
    SyncDockVisibility(shell);

    // 2e. Assign layout to DockControl (LAST)
    DockControl.Layout = layout;

    _logger?.LogInformation("DockControl initialized with {ToolCount} tools, {DocumentCount} documents",
        layout.VisibleDockables.Count, layout.Documents?.Count ?? 0);
}

private void WireViewModels(IRootDock layout, ShellViewModel shell)
{
    // Walk the layout tree and inject Context
    foreach (var dockable in GetDockablesRecursive(layout))
    {
        switch (dockable)
        {
            case ExplorerTool e:
                e.Context = shell.FileExplorerViewModel;
                _logger?.LogDebug("Wired ExplorerTool.Context");
                break;
            case GitTool g:
                g.Context = shell.GitViewModel;
                _logger?.LogDebug("Wired GitTool.Context");
                break;
            case ProblemsTool p:
                p.Context = shell.ProblemsViewModel;
                _logger?.LogDebug("Wired ProblemsTool.Context");
                break;
            case OutputTool o:
                o.Context = shell.OutputViewModel;
                _logger?.LogDebug("Wired OutputTool.Context");
                break;
            case EditorDocument d:
                d.Context = shell.EditorViewModel;
                _logger?.LogDebug("Wired EditorDocument.Context");
                break;
        }
    }
}

private void SyncDockVisibility(ShellViewModel shell)
{
    // Map ShellViewModel booleans to ToolDock visibility
    // LeftToolDock.IsVisible = shell.IsSidebarVisible
    // BottomToolDock.IsVisible = shell.IsBottomPanelVisible
    // ActiveTabIndex maps to ToolDock.ActiveDockable
}
```

### Layout Structure (Freeform Mode)

```
RootDock (ProportionalDock, Orientation=Horizontal)
├── LeftColumn (ProportionalDock, Orientation=Vertical, Proportion=0.25)
│   └── LeftToolDock (ToolDock)
│       ├── ExplorerTool (Context=FileExplorerViewModel)
│       └── GitTool (Context=GitViewModel)
├── Splitter
└── RightColumn (ProportionalDock, Orientation=Vertical, Proportion=0.75)
    ├── DocumentDock (DocumentDock, Proportion=0.70)
    │   └── [dynamic: EditorDocuments opened by FileExplorer]
    ├── Splitter
    └── BottomToolDock (ToolDock, Proportion=0.30)
        ├── ProblemsTool (Context=ProblemsViewModel)
        └── OutputTool (Context=OutputViewModel)
```

---

## Specific Admonitions for Implementation

### 1. Every ProportionalDock child MUST have explicit `Proportion`
```csharp
var root = new ProportionalDock
{
    Orientation = Orientation.Horizontal,
    Children = new DockCollection
    {
        new ProportionalDockItem(leftColumn, 0.25), // ← explicit proportion
        new ProportionalDockItem(rightColumn, 0.75)  // ← explicit proportion
    }
};
```

### 2. Logging from M1 onwards
Add `ILogger<AeroDockFactory>` to factory, log:
- Layout tree structure (recursive walk)
- Number of VisibleDockables per ToolDock
- Context type for each dockable
- Proportion values of all children
- DockControl.InitializeFactory and Factory status

### 3. `DockControl.InitializeFactory = true` BEFORE setting Factory
```csharp
DockControl.InitializeFactory = true;   // Essential!
DockControl.Factory = factory;
DockControl.Layout = layout;            // Last
```

### 4. Preserve ShellViewModel boolean toggles
Do NOT remove `IsSidebarVisible`, `IsBottomPanelVisible`, `ActiveSidebarTabIndex`, `ActiveBottomTabIndex`. These are:
- Used by existing unit tests
- Used by `ToggleSidebarCommand`, `ToggleOutputCommand`, etc.
- Needed for workspace state persistence
- Map to DockControl visibility in `SyncDockVisibility()`

### 5. Test each milestone manually before committing
Run `dotnet run --project src` after each milestone. Use the manual test script `manual_test_manual_test_phase8_docking.sh` if one exists, or create one that checks:
- Explorer panel renders and navigable
- Git panel renders
- Editor opens files
- Problems/Output panels show content
- All toggle commands work
- Window close/open restores layout

---

## Files to Create

| File | Purpose |
|------|---------|
| `src/Docking/Model/ExplorerTool.cs` | IDockable model for Explorer panel |
| `src/Docking/Model/GitTool.cs` | IDockable model for Git panel |
| `src/Docking/Model/ProblemsTool.cs` | IDockable model for Problems panel |
| `src/Docking/Model/OutputTool.cs` | IDockable model for Output panel |
| `src/Docking/Model/AeroRootDock.cs` | Root dock with factory pattern |
| `src/Docking/Model/AeroToolDock.cs` | Tool dock container |
| `src/Docking/DocumentViewModels/EditorDocument.cs` | IDockable model for editor documents |
| `src/Docking/AeroDockFactory.cs` | Layout factory — creates default layout structure |
| `src/Services/LayoutPersistenceService.cs` | Save/load layout JSON (M6) |
| `src/Docking/DockingLogger.cs` | Extension methods for logging dock state |

## Files to Modify

| File | Modification |
|------|-------------|
| `src/App.axaml` | Add Dock.Avalonia.Themes.Simple StyleInclude |
| `src/App.axaml.cs` | Update `OnFrameworkInitializationCompleted` — pass `shell` to `mainWindow.Initialize()`. Change method signature. |
| `src/MainWindow.axaml` | Replace Grid layout with `dock:DockControl`. Add DataTemplates for dockable types. |
| `src/MainWindow.axaml.cs` | Add `InitializeDockControl()`, `WireViewModels()`, `SyncDockVisibility()` methods. Change `Initialize()` signature to accept `ShellViewModel`. |
| `src/ViewModels/ShellViewModel.cs` | No changes needed to existing properties — just ensure `SyncDockVisibility()` maps booleans to DockControl. No new commands needed. |

## Files NOT to Modify

These files MUST remain untouched to preserve existing functionality:
- `src/ViewModels/EditorViewModel.cs` — Document tabs logic unchanged
- `src/ViewModels/FileExplorerViewModel.cs` — File tree logic unchanged
- `src/ViewModels/GitViewModel.cs` — Git operations unchanged
- `src/ViewModels/ProblemsViewModel.cs` — Diagnostic display unchanged
- `src/ViewModels/OutputViewModel.cs` — Build output unchanged
- `src/Services/DocumentManager.cs` — Document lifecycle unchanged
- `src/Views/*.axaml` — All View XAML files unchanged (DataContext comes from Context injection)

---

## Post-Milestone Verification Checklist

After each milestone, verify:
- [ ] `dotnet build src/aero.csproj` succeeds
- [ ] `dotnet test tests` passes (all existing 545 tests)
- [ ] Manual run shows expected panels with content
- [ ] No `$parent[Window].DataContext` bindings anywhere in new code
- [ ] Dock.Avalonia.Themes.Simple StyleInclude is present in App.axaml
- [ ] Every ProportionalDock child has explicit Proportion
- [ ] DataContext is set BEFORE InitializeDockControl() is called
- [ ] `Exit` command still closes cleanly without exceptions
- [ ] Keyboard shortcuts still work (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+W, etc.)