# AgentName_Recommendation.md

> **Agent:** GitHub Copilot  
> **Date:** 2026-06-23  
> **Target:** Phase 8.1a — Dockable Panels Recovery

---

## Approach Overview

This plan follows an **incremental, proof-of-concept-first** approach based on lessons from the Phase 8.1a failure. The key insight from the post-mortem is that the Dock.Avalonia theme was missing from App.axaml — without it, panels render with zero size. The strategy is:

1. **Verify the library works** with a minimal test before any real implementation
2. **Add the missing theme** as the first step (M0.5)
3. **Migrate one panel at a time** with testing after each
4. **Use Option A** (direct Context injection) which was verified in the failed attempt

---

## Recommended DataTemplate Strategy

### Option A — Direct Context Injection (Recommended)

**Justification:**
- Verified to work in the failed attempt — logs showed Context was correctly injected on all 5 tools
- Avoids `$parent[Window].DataContext` binding which breaks inside Dock.Avalonia's DeferredContentControl
- Timing is deterministic: inject Context AFTER DataContext is set in Initialize()

```xml
<!-- App.axaml — DataTemplate with NO binding -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — WireViewModels() called AFTER DataContext set
private void WireViewModels(AeroRootDock layout, ShellViewModel shell)
{
    foreach (var tool in layout.Tools)
    {
        switch (tool)
        {
            case ExplorerTool explorer:
                explorer.Context = shell.FileExplorerViewModel;
                break;
            // ... other tools
        }
    }
}
```

**Option B** (NOT recommended): `{Binding Context}` in DataTemplate — timing uncertain, not verified

---

## Milestone Plan

| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|---------------|--------------|
| **M0.5** | Add Dock.Avalonia.Themes.Simple to App.axaml, verify package installed | Build passes, no runtime errors | `git checkout -- App.axaml` |
| **M1** | Create minimal DockControl with single tool panel (Explorer only) | Explorer renders in DockControl | Revert to Grid layout |
| **M2** | Add AeroDockFactory with single tool (Explorer) | Layout structure correct | Revert M1 |
| **M3** | Wire ExplorerTool.Context to FileExplorerViewModel | Explorer shows file tree | Revert M2 |
| **M4** | Add second tool (Git) to layout | Both Explorer + Git visible | Revert M3 |
| **M5** | Add DocumentDock with EditorDocument | Editor opens files | Revert M4 |
| **M6** | Add bottom panel (Problems + Output) | All 5 panels render | Revert M5 |
| **M7** | Preserve toggle commands (IsSidebarVisible, etc.) | Menu toggles work | Revert M6 |
| **M8** | Add layout persistence stub | Layout saves to settings | Revert M7 |

### M0.5 — Add Dock.Avalonia Theme (Critical)

```xml
<!-- src/App.axaml — Add AFTER SimpleTheme -->
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <!-- ADD THIS LINE -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/SimpleDockTheme.xaml" />
    <!-- ...existing styles -->
</Application.Styles>
```

**Verification:** Build passes, app runs without layout errors

### M1 — Minimal Proof-of-Concept

Replace MainWindow.axaml Grid with single DockControl + one tool:

```xml
<!-- MainWindow.axaml -->
<DockControl x:Name="DockControl">
    <DockControl.Layout>
        <dock:ProportionalDock>
            <dock:ToolDock x:Name="LeftToolDock" />
        </dock:ProportionalDock>
    </DockControl.Layout>
</DockControl>
```

**Test:** Explorer panel renders in its own dockable window (not full-screen)

---

## Initialization Sequence (Pseudo-Code)

```csharp
// src/App.axaml.cs — OnFrameworkInitializationCompleted()
var shell = _services.GetRequiredService<ShellViewModel>();
var mainWindow = new MainWindow { DataContext = shell };  // 1. Set DataContext FIRST
mainWindow.Initialize(bus);                               // 2. Initialize (subscribes bus)
desktop.MainWindow = mainWindow;

// src/MainWindow.axaml.cs
public void Initialize(IMessageBus bus)
{
    _bus = bus;
    _confirmDirtyCloseHandler = OnConfirmDirtyClose;
    _bus.Subscribe(_confirmDirtyCloseHandler);
    // ... other subscriptions
    
    // 3. Initialize DockControl AFTER DataContext is set
    if (DataContext is ShellViewModel shell)
    {
        InitializeDockControl(shell);
    }
}

private void InitializeDockControl(ShellViewModel shell)
{
    // Create layout factory
    var factory = new AeroDockFactory();
    
    // Initialize DockControl
    DockControl.InitializeFactory = true;
    DockControl.Factory = factory;
    
    // Create default layout
    var layout = factory.CreateDefaultLayout();
    
    // Wire ViewModels to Context (Option A)
    WireViewModels(layout, shell);
    
    // Assign layout LAST
    DockControl.Layout = layout;
}

private void WireViewModels(AeroRootDock layout, ShellViewModel shell)
{
    foreach (var tool in layout.Tools)
    {
        switch (tool)
        {
            case ExplorerTool explorer:
                explorer.Context = shell.FileExplorerViewModel;
                break;
            case GitTool git:
                git.Context = shell.GitViewModel;
                break;
            case ProblemsTool problems:
                problems.Context = shell.ProblemsViewModel;
                break;
            case OutputTool output:
                output.Context = shell.OutputViewModel;
                break;
        }
    }
    
    // Documents
    foreach (var doc in layout.Documents)
    {
        if (doc is EditorDocument editor)
            editor.Context = shell.EditorViewModel;
    }
}
```

---

## Key Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|----------|--------|----------|
| **M1: DockControl doesn't render** | High | Blocked | Verify M0.5 theme added first |
| **M1: Single tool fills window** | High | Blocked | Set explicit Proportion on ToolDock |
| **M3: Context null at runtime** | Medium | Blocked | Add debug logging in WireViewModels |
| **M5: EditorDocument not opening** | Medium | Medium | Verify OpenFile command wired |
| **M6: Bottom panel invisible** | High | Blocked | Set Proportion values explicitly |
| **M7: Toggles break** | Low | Medium | Keep existing IsSidebarVisible properties |

### Critical: Proportion Values

All ProportionalDock children MUST have explicit Proportion:

```csharp
// AeroDockFactory.cs
var leftColumn = new AeroProportionalDock
{
    new AeroToolDock
    {
        Dock = Dock.Left,
        Proportion = 0.25,  // 25% width
        // ...
    }
};

var rightColumn = new AeroProportionalDock
{
    new AeroDocumentDock { Proportion = 0.65 },  // 65% width
    new AeroToolDock
    {
        Dock = Dock.Bottom,
        Proportion = 0.10  // 10% height
    }
};
```

---

## Layout Structure Target

```
AeroRootDock (Horizontal)
├── LeftColumn (Vertical, Proportion=0.25)
│   └── ToolDock (Left)
│       ├── ExplorerTool
│       └── GitTool
├── RightColumn (Vertical, Proportion=0.75)
│   ├── DocumentDock (Proportion=0.65)
│   │   └── EditorDocument
│   └── BottomToolDock (Vertical, Proportion=0.10)
│       ├── ProblemsTool
│       └── OutputTool
```

---

## Logging Strategy

Add logging at each milestone to make debugging possible:

```csharp
// M1: Log DockControl initialization
Log.Debug("M1: DockControl initialized, Layout={LayoutType}", 
    DockControl.Layout?.GetType().Name);

// M3: Log Context injection
foreach (var tool in layout.Tools)
{
    Log.Debug("M3: Tool {ToolType}.Context={ContextType}", 
        tool.GetType().Name, 
        tool.Context?.GetType().Name);
}
```

---

## Rollback Strategy

Each milestone is a separate git commit. To rollback:

```bash
# Rollback to M3 (single tool working)
git revert HEAD  # M8
git revert HEAD  # M7
git revert HEAD  # M6
# ... until at M3
```

**Critical rollback point:** M1 — if single tool doesn't render, the theme is missing or DockControl setup is wrong. Do not proceed past M1 until verified.

---

## Summary

| Item | Decision |
|------|---------|
| **DataTemplate Strategy** | Option A — Direct Context injection |
| **Theme** | Add Dock.Avalonia.Themes.Simple in M0.5 |
| **Initialization** | Set DataContext → Initialize() → DockControl |
| **Testing** | One milestone at a time, test after each |
| **Proportion** | Explicit values on all ProportionalDock children |
| **Logging** | From M1 forward |
| **Rollback** | git revert per milestone |

This plan avoids the mistakes of Phase 8.1a by:
1. Adding the missing theme first (M0.5)
2. Testing with a single tool before adding more (M1)
3. Using verified Context injection approach (Option A)
4. Adding logging from the start