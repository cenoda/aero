# GitHubCopilot_Recommendation.md

## Approach Overview

This recommendation proposes a **safe, incremental re-implementation** of Phase 8.1a Dockable Panels using Dock.Avalonia. The strategy prioritizes **validation before implementation** by starting with a minimal proof-of-concept, then incrementally building features with testing after each milestone. The approach uses **Option A (Context injection)** for DataTemplates, which was verified to work in the previous attempt, avoiding the problematic `$parent[Window].DataContext` binding inside Dock.Avalonia's `DeferredContentControl`.

## Recommended DataTemplate Strategy

**Option A: Direct Context Injection** (Recommended)

**Justification:**
1. **Verified working** - In the previous attempt, Context injection was confirmed to work correctly (logs showed all 5 tools had Context wired properly)
2. **Avoids binding chain issues** - `$parent[Window].DataContext` breaks inside `DeferredContentControl` when Dock.Avalonia reconstructs the visual tree
3. **Timing certainty** - Context is set explicitly from code-behind after DataContext is available, avoiding race conditions
4. **Simpler debugging** - Direct property assignment is easier to trace than binding expressions

**Implementation:**
```xml
<!-- App.axaml — DataTemplates -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
    <!-- No DataContext binding — Context injected from code-behind -->
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — WireViewModels()
case ExplorerTool explorer:
    explorer.Context = shell.FileExplorerViewModel;
    break;
```

## Milestone Plan

| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|-------------------|----------------|
| **M0.5** | Proof-of-Concept: Minimal Dock.Avalonia test app | Verify Dock.Avalonia can render a simple tool panel with content | `git commit -m "M0.5: poc"` |
| **M1** | Add Dock.Avalonia theme to App.axaml + verify compilation | App builds, DockControl renders empty layout | `git commit -m "M1: theme"` |
| **M2** | Create AeroDockFactory + Model classes (no ViewModels wired) | Layout structure logged to console, factory creates correct model hierarchy | `git commit -m "M2: models"` |
| **M3** | Wire ViewModels into dockables via Context injection | All 5 tools have Context set, verified via logging | `git commit -m "M3: wire"` |
| **M4** | Replace Grid layout with DockControl in MainWindow | All panels visible and positioned correctly | `git commit -m "M4: layout"` |
| **M5** | Preserve toggle behavior (IsSidebarVisible, etc.) | Toggle commands work, panels hide/show correctly | `git commit -m "M5: toggle"` |
| **M6** | Layout persistence stub + Freeform mode | Layout saves/loads correctly, Freeform mode works | `git commit -m "M6: persist"` |

### M0.5 Detail (Critical — Do Not Skip)

Before any implementation, create a **separate test project** or minimal code in `Program.cs` that:
1. Creates a `DockControl` with a single `ToolDock` containing one tool
2. Sets the tool's `Context` to a simple ViewModel with a string property
3. Verifies the tool renders with content visible
4. **Success criterion:** Panel content is visible and correctly rendered

This validates that Dock.Avalonia works correctly with the current Avalonia 11.3 setup.

## Key Risks & Mitigations

| Risk | Impact | Mitigation |
|------|---------|-------------|
| **Dock.Avalonia rendering still broken** | High — Entire feature fails | M0.5 proof-of-concept catches this early; if fails, investigate Dock.Avalonia version or try alternative library |
| **Context injection timing issues** | Medium — Panels empty | Set Context only after DataContext is set (in `Initialize()`, not constructor) |
| **Proportion values not set** | Medium — Panels have zero size | Explicitly set `Proportion` on all `ProportionalDock` children (default: 0.25, 0.5, 0.25) |
| **Theme not applied correctly** | Medium — Panels invisible | Verify `Dock.Avalonia.Themes.Simple` is in App.axaml (already done in current baseline) |
| **Toggle behavior breaks** | Low — UX regression | Preserve `IsSidebarVisible` etc. as properties on `ShellViewModel`, map to dockable visibility |
| **Layout persistence corrupts state** | Medium — App crashes on startup | Stub persistence first (save/load disabled), enable after layout is stable |

## Initialization Sequence (Pseudo-Code)

### App.axaml.cs (Correct Order)

```csharp
public override void OnFrameworkInitializationCompleted()
{
    _services = BuildServices();
    
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        var bus = _services.GetRequiredService<IMessageBus>();
        
        // 1. Set DataContext FIRST
        var mainWindow = new MainWindow { DataContext = shell };
        
        // 2. Initialize bus subscriptions (DataContext already set)
        mainWindow.Initialize(bus);
        
        desktop.MainWindow = mainWindow;
        // ... rest of initialization
    }
}
```

### MainWindow.axaml.cs (DockControl Setup)

```csharp
public void Initialize(IMessageBus bus)
{
    // 1. Subscribe to bus (existing code)
    _bus = bus;
    // ... existing subscriptions
    
    // 2. Initialize DockControl AFTER DataContext is set
    InitializeDockControl();
}

private void InitializeDockControl()
{
    if (DataContext is not ShellViewModel shell) return;
    
    // 3. Create layout with factory
    var layout = AeroDockFactory.CreateDefaultLayout();
    
    // 4. Configure DockControl
    DockControl.InitializeFactory = true;
    DockControl.InitializeLayout = false;
    DockControl.Factory = layout.Factory!;
    
    // 5. Assign layout to ShellViewModel (for toggle commands)
    shell.ActiveLayout = layout;
    
    // 6. Wire ViewModels into Context (AFTER layout assigned)
    WireViewModels(layout, shell);
    
    // 7. Set Layout LAST (triggers rendering)
    DockControl.Layout = layout;
}

private void WireViewModels(IDockable layout, ShellViewModel shell)
{
    // Recursive walk through layout tree, inject Context
    // Log each injection for debugging
    Console.WriteLine($"[Dock] Wiring ViewModels...");
    
    // Example for ExplorerTool:
    if (layout is ExplorerTool explorer)
    {
        explorer.Context = shell.FileExplorerViewModel;
        Console.WriteLine($"[Dock] ExplorerTool.Context = {shell.FileExplorerViewModel}");
    }
    // ... repeat for GitTool, ProblemsTool, OutputTool, EditorDocument
}
```

## DataTemplate Configuration (App.axaml)

```xml
<Application.Styles>
    <!-- Existing themes -->
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml"/>
    
    <!-- Dock.Avalonia theme (CRITICAL) -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/Themes/Simple.axaml"/>
    
    <!-- DataTemplates for Dock.Avalonia tools/documents -->
    <DataTemplates>
        <!-- Tools -->
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
        
        <!-- Documents -->
        <DataTemplate DataType="dockDocs:EditorDocument">
            <views:EditorView/>
        </DataTemplate>
    </DataTemplates>
</Application.Styles>
```

## Layout Structure (Recommended)

```
RootDock (Horizontal)
├── LeftColumn (ProportionalDock, Proportion=0.25)
│   └── ToolDock (Left)
│       ├── ExplorerTool (Context = FileExplorerViewModel)
│       └── GitTool (Context = GitViewModel)
├── Splitter
└── RightColumn (ProportionalDock, Proportion=0.75)
    ├── DocumentDock (Proportion=0.7)
    │   └── EditorDocument (Context = EditorViewModel)
    ├── Splitter
    └── BottomToolDock (Proportion=0.3, Bottom)
        ├── ProblemsTool (Context = ProblemsViewModel)
        └── OutputTool (Context = OutputViewModel)
```

## Logging Strategy (Add From Start)

Add logging to these locations:
1. `AeroDockFactory.CreateDefaultLayout()` — log layout structure
2. `WireViewModels()` — log each Context injection
3. `MainWindow.InitializeDockControl()` — log initialization steps
4. `ShellViewModel` toggle commands — log visibility changes

Use `Console.WriteLine` or `IMessageBus.Publish(new StatusMessage(...))` for simplicity.

## Verification Checklist (After Each Milestone)

- [ ] Build succeeds (`dotnet build src/aero.csproj`)
- [ ] Tests pass (`dotnet test tests`)
- [ ] App runs without crashes (`dotnet run --project src`)
- [ ] All panels visible and positioned correctly
- [ ] Toggle commands work (sidebar, bottom panel)
- [ ] File explorer opens files in editor
- [ ] No binding errors in console output

---

**Next Step:** Start with M0.5 (Proof-of-Concept) before implementing any production code.
