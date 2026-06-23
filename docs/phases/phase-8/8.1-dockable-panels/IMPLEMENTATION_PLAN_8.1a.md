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
- [ ] `Dock.Model.ReactiveUI` added to `aero.csproj` — provides concrete model types (`RootDock`, `ProportionalDock`, `ToolDock`, `DocumentDock`, `Tool`, `Document`) that ship as instantiable classes, not just interfaces. **Required before M0.5 inline XAML can compile.** Since Aero already uses ReactiveUI, this is the natural fit. Document in `LIBRARIES.md` per AGENTS.md §5.
- [x] No Dock references in `App.axaml` or `MainWindow.axaml`

---

## 2. Architecture

### 2.1 DataTemplate Strategy: Option A (Direct Context Injection)

**Rationale:** The v1 post-mortem logs confirmed Context injection worked — all five tools had their Context set correctly (`[Dock] Wired ExplorerTool.Context` etc. appeared in debug output). The rendering failure was elsewhere. Option A repeats the one thing that was proven to work.

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
| `FactoryBase` | `Dock.Model` | Abstract base class. Extend this instead of implementing from scratch. Note: v1/v2 docs previously called this `Factory` — the actual installed type is `FactoryBase`. |
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

### 2.5 Initialization Sequence (Hypotheses — Verify in M0.5)

> **⚠️ The ordering below is our best inference from the Dock.Avalonia API surface and session notes (`aero-phase8-m2-notes.md`). The specifics of what `OnPropertyChanged` does internally are not public — they are hypotheses to be confirmed in M0.5 when we first assign a layout to `DockControl`. If M0.5 rendering fails, the init sequence is the first thing to investigate.**

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
├─ 2. DockControl.InitializeFactory = true            ← hypothesis: triggers locator setup
├─ 3. DockControl.InitializeLayout = false            ← hypothesis: prevents double init
├─ 4. DockControl.Factory = factory                   ← safety net
├─ 5. var layout = factory.CreateDefaultLayout()      ← hypothesis: calls InitLayout internally
├─ 6. WireViewModels(layout, shell)                   ← Context injection BEFORE layout
├─ 7. DockControl.Layout = layout                     ← LAST — hypothesis: triggers rendering
└─ 8. Log: "init complete, N dockables"
```

**Hypothesis (verify in M0.5):** Setting `DockControl.Layout` triggers internal initialization that reads `InitializeFactory`. If `InitializeFactory` is false at that point, locators (`ContextLocator`, `HostWindowLocator`) are not set up, and rendering/drag-and-drop may fail silently. M0.5 will test this ordering with a minimal XAML spike; the actual working sequence is pinned only after M0.5 passes.

> **⚠️ Init-sequence risk is higher than the original Low rating suggested.** The entire init sequence (§2.5) is unverified. Bump the corresponding risk in §7 to Medium likelihood. If M0.5 fails, init ordering is the first hypothesis to test.

### 2.6 Theme Include (Unverified — Resolve in M0.5)

The `Dock.Avalonia.Themes.Simple` 11.3.12.1 package contains:
- Embedded resource path: `DockSimpleTheme.axaml`
- Class: `Dock.Avalonia.Themes.Simple.DockSimpleTheme` (a `ControlTheme`, not a `ResourceDictionary`)

**The include mechanism is unsettled.** Since `DockSimpleTheme` is a `ControlTheme`, the correct mechanism could be:
- `<StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/DockSimpleTheme.axaml"/>` (if Avalonia resolves ControlThemes via StyleInclude)
- `<ResourceInclude>` in `<Application.Resources>`
- Programmatic: `Application.Styles.Add(new DockSimpleTheme())`

M0.5 step 1 resolves this experimentally. The URI body (`DockSimpleTheme.axaml`) is confirmed; the mechanism is not. **Do not commit a guessed mechanism.**

> **Additional M0.5 note:** The spike XAML also needs the `Dock.Model.ReactiveUI.Controls` namespace for concrete types (see §1 entry gate). This means two `xmlns:` declarations, not one — `Dock.Avalonia.Controls` for `DockControl` and `Dock.Model.ReactiveUI.Controls` for concrete model types.

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

Each item above is a deliberate reduction per plan-rules §7. Do not re-add without a concrete second consumer. Mirror this list in `TOFIX.md` as reductions.

---

## 4. Milestones

### M0.5 — Pure-XAML Rendering Spike

**Goal:** Prove Dock.Avalonia renders inside our app before writing any C# model code. This is the load-bearing gate — if this fails, every later milestone is dead.

**Placement:** The spike goes in the **editor area** (center column), not the sidebar. The sidebar is ~250px wide — a horizontal proportional dock cramped into 250px will look broken even when working, falsely failing the spike. The spike needs ≥600×400 to be a meaningful visual test.

```xml
<!-- MainWindow.axaml — replace EditorView content area with a toggle -->
<!-- Keep EditorView behind IsVisible, add DockSpike alongside it -->
<Grid Grid.Row="0">
    <views:EditorView IsVisible="{Binding !IsSpikeActive}" />
    <!-- M0.5: Dock spike fills the editor area -->
    <ContentControl x:Name="DockSpikeHost" IsVisible="{Binding IsSpikeActive}" />
</Grid>
```

**Steps (order matters — verify before include):**

1. **Verify the theme include mechanism** (§2.6 is unsettled). Also verify `Dock.Model.ReactiveUI` is installed and provides concrete types. Inspect the installed packages:
   ```bash
   # Find the theme embedded resource path
   unzip -l ~/.nuget/packages/dock.avalonia.themes.simple/11.3.12.1/lib/net8.0/*.dll 2>/dev/null | grep -i theme
   # Verify concrete model types exist in Dock.Model.ReactiveUI
   unzip -l ~/.nuget/packages/dock.model.reactiveui/*/lib/net8.0/*.dll 2>/dev/null | grep -i "RootDock\|ToolDock\|DocumentDock"
   ```
   Then try the theme include in `App.axaml`. If `<StyleInclude>` doesn't render the `ControlTheme`, try `<ResourceInclude>` or programmatic `Application.Styles.Add()`. **This step produces the verified mechanism committed in step 2.**

2. **Add the verified theme include** to `App.axaml` (mechanism determined in step 1).

3. **Add the inline XAML spike** to `MainWindow.axaml` in the editor area. The spike uses a `<DockControl>` with **concrete model types from `Dock.Model.ReactiveUI`** — the interfaces (`IRootDock`, `IToolDock`, etc.) ship in `Dock.Model` but are not instantiable. Two xmlns declarations are required: one for the control (`Dock.Avalonia.Controls`) and one for the concrete model types (`Dock.Model.ReactiveUI.Controls`):
   ```xml
   <dock:DockControl x:Name="DockSpike"
                     xmlns:dock="using:Dock.Avalonia.Controls"
                     xmlns:rxctl="using:Dock.Model.ReactiveUI.Controls"
                     InitializeFactory="False" InitializeLayout="False">
       <dock:DockControl.Layout>
           <rxctl:RootDock>
               <rxctl:ProportionalDock Orientation="Horizontal">
                   <rxctl:ProportionalDock Orientation="Vertical" Proportion="0.3">
                       <rxctl:ToolDock Alignment="Left">
                           <rxctl:Tool Id="tool-a" Title="Tool A">
                               <TextBlock Text="Tool A content" Margin="8"/>
                           </rxctl:Tool>
                           <rxctl:Tool Id="tool-b" Title="Tool B">
                               <TextBlock Text="Tool B content" Margin="8"/>
                           </rxctl:Tool>
                       </rxctl:ToolDock>
                   </rxctl:ProportionalDock>
                   <rxctl:ProportionalDockSplitter/>
                   <rxctl:ProportionalDock Orientation="Vertical" Proportion="0.7">
                       <rxctl:DocumentDock>
                           <rxctl:Document Id="doc-a" Title="Doc A">
                               <TextBlock Text="Doc A content" Margin="8"/>
                           </rxctl:Document>
                       </rxctl:DocumentDock>
                   </rxctl:ProportionalDock>
               </rxctl:ProportionalDock>
           </rxctl:RootDock>
       </dock:DockControl.Layout>
   </dock:DockControl>
   ```
   > **Why `InitializeFactory="False"`:** For a pure-XAML spike where you just want the static layout to render, `False` is the safer default — it asks the control to render what's there without invoking factory machinery. Try this first; if rendering works, you've also confirmed the factory path is independent of basic rendering. If rendering fails, flip to `True` as a hypothesis.
   > **Note:** This XAML tests whether the concrete ReactiveUI types work as inline content holders in 11.3. If they don't (e.g. the library requires factory-created instances), M0.5 catches that here — before any C# is written.
   > **Note:** `xmlns` declarations above are for illustration — move them to the root `<Window>` tag if Avalonia XAML scoping requires it.

4. **Add `IsSpikeActive` plumbing** to `ShellViewModel`:
   - Add `[Reactive] public bool IsSpikeActive { get; set; }` (default `false`)
   - Add `ToggleSpikeCommand` bound to a View menu item or keyboard shortcut (e.g. `Ctrl+Shift+D`)
   - This property is **removed in M3 step 5** when the spike is replaced by the real DockControl
   - The XAML binding `IsVisible="{Binding IsSpikeActive}"` on the spike ContentControl and `IsVisible="{Binding !IsSpikeActive}"` on EditorView now resolve correctly

5. **Build and run.** Click to show the spike tab.

**Verification (acceptance criteria):**
- `dotnet build src/aero.csproj` — 0 errors
- Spike area shows **both** tool tabs labelled "Tool A" and "Tool B" with their TextBlock text visible
- Document area shows "Doc A content"
- Drag the splitter — both regions resize
- **If any of these are not visible, STOP.** Theme, package version, or Avalonia 11.3 compat is the cause; no later milestone can succeed until this passes.

**Rollback:** `git tag v2-m0.5-spike`

---

### M1 — Model Classes + Factory

**Goal:** Replace M0.5 inline XAML with factory-driven layout, still inside the spike area.

**Files to create:**

| File | Purpose |
|------|---------|
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
2. Create `AeroDockFactory : FactoryBase` with `CreateDefaultLayout()` building the tree from §2.4
3. Add `Application.DataTemplates` in `App.axaml` (see §2.1)
4. Replace M0.5 inline XAML with `<dock:DockControl x:Name="DockSpike"/>`
5. Wire factory + layout in `MainWindow.axaml.cs` (see §2.5)
6. Add `[Dock]`-prefixed debug logging

**Verification:**
- Spike area shows layout driven by factory (same visual as M0.5)
- Log line `[Dock] init: factory assigned` appears in debug output
- Layout tree dump appears in debug output (depth, type, id, proportion)
- `dotnet test tests` — 527 pass (no new tests in M1)

**Rollback:** `git tag v2-m1-factory`

---

### M2 — Wire Real ViewModels

**Goal:** Spike area shows real content — file tree, Git panel, editor, problems, output.

**Steps:**
1. Implement `WireViewModels()` (see §2.1) in `MainWindow.axaml.cs`
2. `EnumerateDockables()` recursively walks `IRootDock` via `IDock.Dockables`
3. Each tool's `Context` is set to the corresponding ShellViewModel property

> **Known M2 condition:** During M2, the spike DockControl contains an EditorDocument wired to EditorViewModel. The existing Grid layout also hosts an EditorView wired to the same EditorViewModel. Two AvaloniaEdit instances on one document model is allowed in MVVM, but focus/caret behavior with two editors on one VM is not something this codebase has exercised. Clicking a file in the spike's Explorer will also open it in the Grid editor. **This is a known condition resolved by M3** when only one mode is visible at a time.\n>\n> **⚠️ Escape valve:** If M2 hangs or crashes due to dual-editor state (two AvaloniaEdit instances on one VM), **roll forward to M3 immediately** rather than debugging in M2. M3 makes Grid and Freeform mutually exclusive, eliminating the dual-editor condition. Spending time debugging a condition that M3 resolves is wasted effort.

**Verification:**
- Spike area: Explorer tree expands, Git shows changes, Problems lists diagnostics, Output shows build log
- Click file in Explorer → opens in Editor within the spike area (note: also opens in Grid editor — expected in M2)
- `dotnet test tests` — 527 pass (no new tests in M2)

**Rollback:** `git tag v2-m2-wired`

---

### M3 — Promote DockControl to Window Region (LayoutMode Switch)

**Goal:** DockControl available alongside the existing Grid. Mode switchable from View menu.

**Files to create:**
| File | Purpose |
|------|---------|
| `src/Docking/LayoutMode.cs` | `enum LayoutMode { Grid, Freeform }` |

> `LayoutMode.cs` lands here (not M1) because the enum has no consumer until M3. Creating it in M1 would be YAGNI.

**Steps:**
1. Add `LayoutMode` property to `ShellViewModel`
2. Add "Layout Mode" menu item under View
3. In `MainWindow.axaml`: Grid (existing) + DockControl, controlled by `IsVisible`
4. On mode switch: if switching to Freeform and dock not initialized, call `InitializeDockControl()`
5. Remove the M0.5/M1/M2 spike — DockControl now fills the editor region
   > **Also remove `IsSpikeActive`** from `ShellViewModel` (added in M0.5 step 4) and delete the spike XAML from `MainWindow.axaml`. The spike served its purpose and is no longer needed.

**Verification:**
- App starts in Grid mode (unchanged behavior)
- View → Freeform → Explorer in left sidebar (tab 0), Git accessible from sidebar tab 1, Editor in center with current open files, Problems and Output as tabs in the bottom dock
- View → Grid → original layout returns, identical to pre-8.1a
- Both modes use the same `ShellViewModel`
- `dotnet test tests` — 527 pass (no new tests in M3)

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
- `dotnet test tests` — 527 pass (existing tests exercise the booleans, which remain source of truth)

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
   > **⚠️ Hypothesis:** Using `IRootDock` (interface) as the type parameter may require `[JsonDerivedType]` discriminators or a concrete root type for polymorphic deserialization. **Try `DockSerializer<IRootDock>` first;** if System.Text.Json source-gen fails, fall back to `DockSerializer<AeroRootDock>` (concrete) or a wrapper `LayoutEnvelope` type. Document whichever works.
3. Atomic write: `.tmp` file then `File.Move(overwrite: true)`
4. Corrupt file handling: try/catch → delete → return null → fallback to default
5. Schema version field in JSON for forward-compat
6. Register in DI (singleton)
7. Save on `MainWindow.OnClosing`; load only when `LayoutMode == Freeform`
   > **OnClosing integration:** The existing `MainWindow.OnClosing` already runs `await shell.CheckDirtyBeforeExitAsync()` then `await shell.SaveWorkspaceStateAsync()`. Add layout save **after** `SaveWorkspaceStateAsync`, in the same try/catch — a layout save failure should not block the close.
   > **WorkspaceState vs LayoutPersistenceService:** `WorkspaceState` serializes window position, open files, and recent folders. `LayoutPersistenceService` adds dock arrangement to a separate file (`~/.aero/layout.json`). Two persisters, two failure modes. This split is intentional: workspace state is stable and tested; layout persistence is new and experimental. If layout persistence proves reliable, it could be folded into `WorkspaceState` in a future phase.

**Verification:**
- Launch → Freeform → rearrange → close → relaunch → preserved
- Corrupt JSON → relaunch → "Layout reset" status, default loaded
- Grid mode → no layout file loaded
- `dotnet test tests` — 527 pass (LayoutPersistenceService may add new tests, bringing total above 527)

**Rollback:** `git tag v2-m5-persist`

---

### M6 — Default Flip + Cleanup

**Goal:** Freeform becomes the default. Phase 8.1a complete.

**Steps:**
1. Change `LayoutMode` default from `Grid` to `Freeform`
2. Create `manual_test_phase8_1a.sh` — the script must exercise these smoke items (follow the pattern from existing `manual_test_*.sh` scripts):
   - App launches without errors (exit 0)
   - All 5 panels render visible content (Explorer, Git, Editor, Problems, Output)
   - All 5 View menu toggles fire and return to expected state (sidebar hide/show, bottom panel hide/show, output tab)
   - Layout persists across restart: rearrange panels → close → relaunch → verify arrangement
   - Mode switch round-trip: Freeform → Grid → Freeform, all content still visible
   - All keybindings fire: `Ctrl+Shift+E` (Explorer), `Ctrl+`` ` (Output), etc.
   - Script prints PASS/FAIL for each item with exit code 0 (all pass) or 1 (any fail)
3. Update `docs/roadmap/PHASES.md` — mark 8.1a complete
4. Record scope reductions in `docs/TOFIX.md`
5. Clean up unused imports and debug code

**Verification:**
- `dotnet build src/aero.csproj` — 0 errors, 0 new warnings
- `dotnet test tests` — all pass (527 baseline, or higher if M5 added persistence tests)
- `manual_test_phase8_1a.sh` passes smoke checklist
- `docs/roadmap/PHASES.md` 8.1a checked off

**Rollback:** `git tag v2-m6-default-freeform`

---

## 5. Files Summary

### Files to Create

| File | Milestone | Purpose |
|------|-----------|---------|
| `src/Docking/AeroDockFactory.cs` | M1 | `FactoryBase` subclass — creates all dock model types |
| `src/Docking/LayoutMode.cs` | M3 | `enum LayoutMode { Grid, Freeform }` — deferred from M1 (YAGNI until M3) |
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
| `src/MainWindow.axaml` | M0.5 | Add spike area in editor region (temporary) |
| `src/MainWindow.axaml` | M3 | Add Grid/DockControl switch structure |
| `src/MainWindow.axaml.cs` | M1 | Add `InitializeDockControl()`, `WireViewModels()`, `EnumerateDockables()` |
| `src/MainWindow.axaml.cs` | M3 | Wire LayoutMode switch |
| `src/MainWindow.axaml.cs` | M4 | Add `SyncDockVisibility()`, `FindDockable()` |
| `src/MainWindow.axaml.cs` | M5 | Wire layout persistence on close |
| `src/ViewModels/ShellViewModel.cs` | M0.5 | Add `IsSpikeActive` property + `ToggleSpikeCommand` (removed in M3) |
| `src/ViewModels/ShellViewModel.cs` | M3 | Add `LayoutMode` property, `IsFreeformMode` computed, remove `IsSpikeActive` |
| `src/ViewModels/ShellViewModel.cs` | M4 | Rewrite toggle commands to operate per mode |
| `src/ViewModels/ShellViewModel.cs` | M6 | Change default to Freeform |
| `src/App.axaml.cs` | M5 | Register `LayoutPersistenceService` in DI |
| `src/aero.csproj` | M0.5 | Add `Dock.Model.ReactiveUI` package reference |

### Files NOT to Modify

- `src/ViewModels/ShellViewModel.cs` — `IsSpikeActive` is **temporary** (M0.5–M3 only); `LayoutMode` added in M3; toggle commands rewritten in M4; default changed in M6
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
| Concrete model types missing from installed packages | High | Critical (blocks M0.5) | Add `Dock.Model.ReactiveUI` to entry gate; document in `LIBRARIES.md` |
| Theme URI wrong | Medium | Critical | M0.5 verifies by inspecting package; spike empty → STOP |
| Dock.Avalonia 11.3 undocumented behavior | Medium | High | M0.5 is purely XAML against exact installed version |
| Init sequence (§2.5) is hypothesis | Medium | High | M0.5 is the experiment; pin the verified ordering in §2.5 only after M0.5 passes |
| DockSerializer<IRootDock> unverified polymorphic serialization | Medium | High | Try `DockSerializer<IRootDock>` first; fall back to concrete type or wrapper |
| Grid replacement breaks keybindings | Medium | Medium | LayoutMode switch keeps Grid alive; M4 wires per mode |
| Layout JSON corrupts on shutdown | Medium | High | M5 try/catch + version check; atomic write; delete corrupt |
| Toggle commands drift between modes | Medium | Medium | Reactive booleans remain canonical; mode code pushes to dock |
| M2 dual-editor state hangs or crashes | Medium | Medium | If M2 hangs/crashes due to two AvaloniaEdit instances on one VM, **roll forward to M3 immediately** rather than debugging in M2. M3 makes modes mutually exclusive. |

---

## 8. Definition of Done

All must be true before declaring 8.1a complete:

- [ ] `dotnet build src/aero.csproj` — 0 errors, 0 new warnings
- [ ] `dotnet test tests` — all pass (527 baseline, or higher if M5 added persistence tests)
- [ ] `manual_test_phase8_1a.sh` passes smoke checklist
- [ ] `LayoutMode` defaults to `Freeform`; switch to Grid and back works
- [ ] All 5 panels render real content in Freeform mode
- [ ] All keyboard shortcuts still fire
- [ ] `docs/roadmap/PHASES.md` 8.1a checked off
- [ ] `docs/TOFIX.md` has no unchecked items blocking 8.1a
- [ ] Any scope reductions recorded with rationale
