# GitHub Copilot — Phase 8.1a Dockable Panels Recommendation

## Milestone Plan

### M0.5 — Baseline Setup (Completed)
- Confirm current branch: `phase-8.1a-dockable-panels-v2`
- Verify all 545 tests pass
- Confirm `App.axaml` includes `Dock.Avalonia.Themes.Simple` StyleInclude

### M1 — Minimal DockControl Validation
1. Create test window with single ExplorerTool
2. Use Option A: Inject ViewModel into `IDockable.Context`
3. Verify basic docking works
4. Add logging to track DataContext flow

### M2 — Multi-Panel Integration
1. Add GitTool to same ToolDock as Explorer
2. Implement ProportionalDock with explicit proportions
3. Add OutputTool to Bottom ToolDock
4. Validate panel visibility and resizing

### M3 — Editor Document Integration
1. Add EditorDocument to DocumentDock
2. Implement tabbed document support
3. Test document switching
4. Verify editor content rendering

### M4 — Final Integration & Testing
1. Add all panels to layout
2. Implement toggle commands in ShellViewModel
3. End-to-end testing with all panels
4. Document troubleshooting steps

## DataTemplate Strategy (Option A)

```xml
<!-- ExplorerTool Template -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView />
</DataTemplate>

<!-- GitTool Template -->
<DataTemplate DataType="dockTools:GitTool">
    <views:GitView />
</DataTemplate>
```

**Implementation:**
1. Remove all DataContext bindings from DataTemplates
2. Inject ViewModels directly into `IDockable.Context` in code-behind
3. Use `DataContext = ((IDockable)this).Context` in views

## Initialization Sequence

```csharp
// MainWindow.cs
public partial class MainWindow : Window
{
    private IMessageBus? _bus;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public void Initialize(IMessageBus bus)
    {
        _bus = bus;
        // 1. Set DataContext first
        DataContext = new ShellViewModel(bus);
        // 2. Initialize DockControl after DataContext set
        InitializeDockControl(bus);
        // 3. Wire view models
        WireViewModels();
        // 4. Assign layout
        AssignLayout();
    }

    private void InitializeDockControl(IMessageBus bus)
    {
        // Implementation from DOCKING_APPROACH_ANALYSIS.md
    }
}
```

## Risk Mitigations

1. **Incremental Validation**
   - Test each panel individually before combining
   - Use test window for isolated validation

2. **Early Logging**
   - Add debug output in DataTemplate creation
   - Log DataContext changes in ShellViewModel

3. **Theme Verification**
   - Confirm `Dock.Avalonia.Themes.Simple` in App.axaml
   - Add theme validation in App.Initialize()

4. **DeferredContentControl Debugging**
   - Add visual indicators in test templates
   - Use Live Visual Tree debugging

5. **Initialization Order**
   - Strict sequence: DataContext → DockControl → ViewModels → Layout
   - Add validation checks at each step

See POSTMORTEM-phase-8.1a.md for failure patterns to avoid.