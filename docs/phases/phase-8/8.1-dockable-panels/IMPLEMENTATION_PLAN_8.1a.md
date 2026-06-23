# 8.1a — Dockable Panels (Freeform Mode): Implementation Plan

> **Status:** ✅ Complete — M1–M7 all implemented and verified (2026-06-23)
> **Author:** Kiro (via GitHub Copilot)
> **Parent:** [`README.md`](./README.md)
> **Depends on:** 8.9 Design System ✅, 8.5 Icon Decision ✅

---

## 0. Entry Gates (M0)

All must be true before coding starts:

- [x] Phase 8 TOFIX items R1.1–R1.4 all closed
- [x] `dotnet build src/aero.csproj` passes (0 errors) — confirmed 2026-06-22
- [x] `dotnet test tests` passes (baseline: 527 passed)
- [x] 8.9 Design System complete — all `src/Styles/` tokens wired in `App.axaml`
- [x] 8.5 Icon Decision complete — `Icons.axaml` + `IconResolver.cs` in place
- [x] `Dock.Avalonia 11.3.*` + `Dock.Serializer.SystemTextJson 11.3.*` in `aero.csproj`
- [x] Dock.Avalonia net8.0 fallback verified — smoke test passed (TOFIX R1.2)
- [x] Dock.Serializer API confirmed — `DockSerializer<T>` with `[DockJsonSerializable]` (TOFIX R1.3)

---

## 1. Current State

### Layout (fixed Grid, to be replaced)

```
Window
└── DockPanel
    ├── Menu (Top)
    └── Grid (3 columns)
        ├── Col 0: Sidebar (250px) — TabControl [Explorer, Git]
        ├── Col 1: GridSplitter (4px)
        └── Col 2: Grid (3 rows)
            ├── Row 0: EditorView (*)  — tabs + AvaloniaEdit
            ├── Row 1: GridSplitter (4px)
            ├── Row 2: Bottom Panel (TabControl [Problems, Output], H=150)
            └── Row 3: Status Bar (Auto)
```

### Panel ViewModels (all exist, all functional)

| Panel | ViewModel | Location |
|-------|-----------|----------|
| File Explorer | `FileExplorerViewModel` | Sidebar tab 0 |
| Git Panel | `GitViewModel` | Sidebar tab 1 |
| Editor | `EditorViewModel` | Center |
| Problems | `ProblemsViewModel` | Bottom tab 0 |
| Output | `OutputViewModel` | Bottom tab 1 |

### ShellViewModel panel state (to be replaced by Dock model)

- `IsSidebarVisible` → replaced by Dock layout persistence
- `ActiveSidebarTabIndex` → replaced by Dock tab grouping
- `IsBottomPanelVisible` → replaced by Dock layout persistence
- `ActiveBottomTabIndex` → replaced by Dock tab grouping

### Key constraints

- Dock.Avalonia is fully installed but zero code references exist in `src/`
- `DialogHost.Avalonia` is incompatible with Avalonia 11.3 — custom overlays only
- All existing toggle commands (`ToggleSidebarCommand`, etc.) must continue working

---

## 2. Scope

### In scope (8.1a only)

- **Dock.Avalonia infrastructure**: `DockControl`, `Dock` layout root, `DockableControl`
- **5 panel dockable wrappers**: Each existing panel gets a `DockableContent` wrapper that holds its `UserControl`
- **Drag-and-drop rearrangement**: Panels can be dragged between docking zones (left, right, center, bottom)
- **Panel hiding**: Close button on panel headers; toggle commands restore them
- **Layout persistence**: Save/restore layout to `~/.aero/layout.json` via `Dock.Serializer.SystemTextJson`
- **Mode switch stub**: A `LayoutMode` enum (Tile/Freeform) in settings; 8.1a only implements Freeform
- **View menu updates**: "Toggle Sidebar", "Toggle Bottom Panel", etc. now toggle Dock layout visibility

### Out of scope (deferred)

- **8.1b Tile Mode** — auto-layout with tiling + stack (next sub-phase)
- **8.1c Tear-away windows** — drag panels out to standalone windows (lowest priority)
- **New panels** — no new panels created; only existing 5 are converted
- **Panel ordering/pinning** — user can't pin panels to specific positions yet

---

## 3. Architecture

### 3.1 Dock.Avalonia Concepts (verified against 11.3.12.1 API)

| Concept | Namespace | Aero mapping |
|---------|-----------|-------------|
| `DockControl` | `Dock.Avalonia.Controls` | Root `TemplatedControl` hosting the layout in `MainWindow.axaml`. Creates internal `DockManager`. |
| `IFactory` | `Dock.Model.Core` | Factory that creates layout model types (`IRootDock`, `ITool`, etc.). Must be implemented to register Avalonia `DataTemplate`s. |
| `IDock` | `Dock.Model.Core` | Any node in the layout tree (containers + dockables). |
| `IRootDock` | `Dock.Model.Controls` | Top-level `IDock` container. Has `IsMaximized`, `Window`, `Windows` properties. |
| `ITool` | `Dock.Model.Controls` | Interface for side/panel dockables (Explorer, Git, Problems, Output). |
| `IToolDock` | `Dock.Model.Controls` | Container that holds `ITool` dockables (sidebar zone, bottom zone). Has `Alignment` (Left/Right/Top/Bottom). |
| `IDocument` | `Dock.Model.Controls` | Interface for document/content dockables (Editor). |
| `IDocumentDock` | `Dock.Model.Controls` | Container that holds `IDocument` dockables (center zone). |
| `IProportionalDock` | `Dock.Model.Controls` | Container that arranges children proportionally with splitters. Used for zone layout. |
| `IProportionalDockSplitter` | `Dock.Model.Controls` | Splitter between proportional dock children. |
| `DockManager` | `Dock.Model` | Implements drag-and-drop docking algorithms. Created internally by `DockControl`. |
| `DockableControl` | `Dock.Avalonia.Controls` | Internal state tracker for `IDockable` — not a wrapper. Tracks bounds, drag state. |
| `DockSerializer<T>` | `Dock.Serializer.SystemTextJson` | JSON serialization with `[DockJsonSerializable]` attribute on model types. |

### 3.2 Layout Model

```
IRootDock (layout root)
├── IProportionalDock (Horizontal, splits left | right)
│   ├── IProportionalDock (Vertical, left column)
│   │   ├── IToolDock (Alignment.Left, Proportion=0.25)  ← sidebar
│   │   │   ├── ITool: FileExplorer
│   │   │   └── ITool: Git
│   │   └── IProportionalDockSplitter
│   └── IProportionalDock (Vertical, right column)
│       ├── IDocumentDock (Proportion=*)  ← editor
│       │   └── IDocument: Editor
│       └── IProportionalDock (Vertical, bottom section)
│           ├── IToolDock (Alignment.Bottom, Proportion=0.3)  ← bottom panel
│           │   ├── ITool: Problems
│           │   └── ITool: Output
│           └── IProportionalDockSplitter
```

> **Note:** The exact tree structure will be determined during M1 implementation. Dock.Avalonia's `IProportionalDock` with `Orientation.Horizontal/Vertical` and `IProportionalDockSplitter` create the zone layout. `IToolDock` with `Alignment` determines docking direction.

### 3.3 New Files to Create

| File | Purpose |
|------|---------|
| `src/Docking/AeroDockFactory.cs` | `IFactory` implementation. Creates layout model types and registers Avalonia `DataTemplate`s that bind panel ViewModels to their `UserControl`s. |
| `src/Docking/LayoutPersistenceService.cs` | Save/restore `IRootDock` to `~/.aero/layout.json` via `DockSerializer<IRootDock>`. |
| `src/Docking/LayoutMode.cs` | `enum LayoutMode { Freeform, Tile }` |
| `src/Docking/ToolViewModels/` | `ITool` implementations wrapping each panel ViewModel (e.g. `ExplorerTool`, `GitTool`, `ProblemsTool`, `OutputTool`). |
| `src/Docking/DocumentViewModels/` | `IDocument` implementation wrapping `EditorViewModel` (e.g. `EditorDocument`). |

### 3.4 Files to Modify

| File | Changes |
|------|---------|
| `src/MainWindow.axaml` | Replace Grid layout with `<DockControl>` + register `DataTemplate`s |
| `src/MainWindow.axaml.cs` | Create `AeroDockFactory`, build initial layout, assign to `DockControl.Layout`; wire layout persistence on close |
| `src/ViewModels/ShellViewModel.cs` | Remove `IsSidebarVisible`, `IsBottomPanelVisible`, `ActiveSidebarTabIndex`, `ActiveBottomTabIndex`. Toggle commands now find dockables by ID in the `IRootDock` tree and toggle `IsVisible`. |
| `src/App.axaml.cs` | Register `LayoutPersistenceService` in DI |

---

## 4. Milestones

### M0.5 — Pre-Implementation Verification (New)

**Goal:** Resolve the critical uncertainties flagged by reviews before writing any implementation code.

**Steps:**
1. **Verify `IFactory` concrete return types:**
   - Dock.Model only has `FactoryBase` (abstract). No `Dock.Model.Mvvm` package exists in the dependency graph.
   - `IFactory` methods return interfaces (`IRootDock`, `ITool`, etc.). The concrete types are in `Dock.Avalonia.dll` itself.
   - **Check `ManagedDockableBase`** in `Dock.Avalonia.Controls` — this is the likely base class for custom `ITool`/`IDocument` implementations (confirmed present in DLL). If it's `internal`, implement `ITool`/`IDocument` directly with `INotifyPropertyChanged` property notification.
   - **Approach:** Extend `FactoryBase` or implement `IFactory` directly. Test in a spike to confirm which concrete types are available and public.
   - **Fallback:** If `FactoryBase` is usable, extend it. If not, implement `IFactory` from scratch using only the interface types (Dock.Avalonia resolves them via DataTemplates).
2. **Verify DataTemplate approach:**
   - Use XAML `DataTemplate` in `Window.DataTemplates` — Avalonia-idiomatic, explicit, no magic.
   - **Do NOT use `AutoCreateDataTemplates`** — remove all references from the plan. This is a Dock.Avalonia convenience that may conflict with our custom templates.
   - DataTemplate maps: `ExplorerTool` → `FileExplorerView`, etc.
3. **Verify Dock init sequence:**
   - `DockControl.InitializeFactory = true` — wires the factory to the control. **Keep this.**
   - `DockControl.InitializeLayout = true` — tells Dock to build its own default layout. **Do NOT set this when providing a manual layout** — it would overwrite the layout you assign. Only use when you want Dock to create the layout from scratch.
   - Correct pattern: create layout via factory → set `DockControl.Layout = layout` → set `DockControl.InitializeFactory = true` → do NOT set `InitializeLayout`.
   - Verify in spike that this sequence works correctly.
4. **Verify dockable lookup APIs:**
   - `IDock.Dockables` and `IDock.VisibleDockables` are `IList<IDockable>` — walk them recursively for toggle commands.
   - `DockManager` may have `FindDockable` — check at runtime. If not, custom tree walker is acceptable since the tree is small (5 panels).
5. **Create `tests/Docking/` folder** with test class stubs.

**Deliverable:** Verified API approach documented. Spike confirms `IFactory` implementation works. Test folder created.

**Test:** Spike compiles and renders a minimal `DockControl` with one panel.

---

### M1 — Dock Infrastructure Skeleton

**Goal:** Replace the Grid layout with a `DockControl` that renders a dockable layout tree.

**Steps:**
1. Create `src/Docking/LayoutMode.cs`:
   ```csharp
   public enum LayoutMode { Freeform, Tile }
   ```
2. Create `src/Docking/AeroDockFactory.cs` — implements `IFactory`:
   - Overrides `CreateRootDock()`, `CreateToolDock()`, `CreateDocumentDock()`, `CreateProportionalDock()`, `CreateTool()`, `CreateDocument()`, `CreateProportionalDockSplitter()`
   - Each override returns the concrete implementation from `Dock.Avalonia` or `Dock.Model` (verified in M0.5 — no `Dock.Model.Mvvm` package exists, use `FactoryBase` or implement `IFactory` directly)
   - Registers `DataTemplate`s that map `ITool`/`IDocument` view models to their `UserControl` views:
     ```csharp
     // In the factory, store DataTemplate references:
     // ExplorerTool → FileExplorerView
     // GitTool → GitPanelView
     // EditorDocument → EditorView
     // ProblemsTool → ProblemsView
     // OutputTool → OutputView
     ```
   - Implements `InitLayout(IDock layout)` — sets up default locators and context
3. Create `src/Docking/ToolViewModels/ExplorerTool.cs` — minimal `ITool` implementation:
   ```csharp
   // Base class TBD by M0.5 spike — likely ManagedDockableBase or direct interface impl
   public class ExplorerTool : ITool  // implement INotifyPropertyChanged for bindable props
   {
       public string Id { get; set; } = "Explorer";
       public string Title { get; set; } = "Explorer";
       // ... other ITool members (Dock, Owner, IsVisible, etc.)
   }
   ```
   Similarly: `GitTool`, `ProblemsTool`, `OutputTool`.
   > **Note:** Exact base class determined in M0.5. If `ManagedDockableBase` is public, extend it. Otherwise implement `ITool` directly.
4. Create `src/Docking/DocumentViewModels/EditorDocument.cs` — minimal `IDocument` implementation:
   ```csharp
   // Base class TBD by M0.5 spike
   public class EditorDocument : IDocument
   {
       public string Id { get; set; } = "Editor";
       public string Title { get; set; } = "Editor";
       // ... other IDocument members
   }
   ```
5. Update `src/MainWindow.axaml`:
   - Remove the Grid (columns 0-2), GridSplitters, and all panel content
   - Add `xmlns:dock="using:Dock.Avalonia.Controls"` namespace
   - Add `<dock:DockControl x:Name="DockControl" />` as the DockPanel content
   - Register `DataTemplate`s for each tool/document view model type:
     ```xml
     <Window.DataTemplates>
         <DataTemplate DataType="{x:Type local:ExplorerTool}">
             <views:FileExplorerView/>
         </DataTemplate>
         <DataTemplate DataType="{x:Type local:GitTool}">
             <views:GitPanelView/>
         </DataTemplate>
         <!-- etc. -->
     </Window.DataTemplates>
     ```
6. Update `src/MainWindow.axaml.cs`:
   - In `OnInitialized`:
     ```csharp
     var factory = new AeroDockFactory();
     var layout = factory.CreateDefaultLayout();
     DockControl.Layout = layout;        // assign our layout FIRST
     DockControl.InitializeFactory = true; // wire the factory
     // Do NOT set InitializeLayout = true — that tells Dock to build its own default
     // and would overwrite our manually-created layout. Verify in M0.5 spike.
     ```

**Deliverable:** IDE starts with a `DockControl` rendering a layout tree. Panels may be empty initially but the dock framework is active.

**Test:** Manual — app starts, no crash, `DockControl` renders (may show empty chrome).

---

### M2 — Wrap Existing Panels as Dockables

**Goal:** All 5 existing panels render inside Dock.Avalonia dockable containers via DataTemplates.

**Steps:**
1. Wire each tool/document ViewModel's content through `DataTemplate`:
   - `ExplorerTool.DataContext` → `FileExplorerViewModel` (set by factory or MainWindow)
   - `GitTool.DataContext` → `GitViewModel`
   - `ProblemsTool.DataContext` → `ProblemsViewModel`
   - `OutputTool.DataContext` → `OutputViewModel`
   - `EditorDocument.DataContext` → `EditorViewModel`
2. Build the full layout tree in `AeroDockFactory.CreateDefaultLayout()`:
   ```csharp
   // Horizontal ProportionalDock: left zone | right zone
   //   Left: IToolDock(Alignment.Left) { ExplorerTool, GitTool }
   //   Right: Vertical ProportionalDock:
   //     Top: IDocumentDock { EditorDocument }
   //     Bottom: IToolDock(Alignment.Bottom) { ProblemsTool, OutputTool }
   ```
3. Wire panel data contexts in `MainWindow.axaml.cs`:
   - After creating the layout, find each tool by ID and set its `DataContext` to the corresponding ViewModel
   - The `DataTemplate` registered in XAML renders the `UserControl` when the tool is visible
4. Register `DataTemplate`s in `Window.DataTemplates` (do NOT use `AutoCreateDataTemplates` — explicit templates are preferred for clarity and control)

**Deliverable:** All 5 panels visible in their default positions. Sidebar has Explorer+Git tabs. Bottom has Problems+Output tabs. Editor fills center. Drag-and-drop works by default (built into `DockControl`).

**Test:** Manual — all panels render with correct content. Tabs switch between Explorer/Git and Problems/Output. Basic drag between zones works.

---

### M3 — Drag-and-Drop Rearrangement

**Goal:** User can drag panels between docking zones (left ↔ center ↔ bottom ↔ right).

**Steps:**
1. Dock.Avalonia's `DockControl` provides built-in drag-and-drop via `DockManager` + `DockControlState`. No custom code needed for basic drag.
2. Verify dock behavior with our `ITool`/`IDocument` models:
   - Drag from left `IToolDock` to bottom `IToolDock` → tool moves
   - Drag from bottom `IToolDock` to left `IToolDock` → tool moves
   - Drag `IDocument` out of `IDocumentDock` → creates floating `IDockWindow`
3. `DockControl.IsDockingEnabled` (default `true`) controls whether docking interactions are active
3. XAML `DataTemplate`s registered in M1/M2 handle rendering — no `AutoCreateDataTemplates` needed
5. Handle edge cases:
   - Drag last tool out of an `IToolDock` → zone collapses (empty `IToolDock` hides)
   - Drag tool back into collapsed zone → zone re-expands
   - Tab grouping: drag tool onto another tool's tab strip → creates tab group within same `IToolDock`

**Deliverable:** All 5 panels can be freely rearranged by dragging.

**Test:** Manual — drag each panel to every other zone. Verify no crashes, visual feedback appears, tabs group correctly.

---

### M4 — Panel Visibility Toggle Commands

**Goal:** Existing View menu commands (Toggle Sidebar, Toggle Output, etc.) work with Dock layout.

**Steps:**
1. Update `ShellViewModel`:
   - Remove `IsSidebarVisible`, `ActiveSidebarTabIndex`, `IsBottomPanelVisible`, `ActiveBottomTabIndex`
   - Inject `ILayoutPersistenceService` to get the active layout (keeps ViewModel decoupled from MainWindow per AGENTS.md MVVM rules)
   - Rewrite toggle commands to navigate the dock tree and toggle `IsVisible` on the appropriate `IDock`:
     ```csharp
     // ToggleSidebarCommand → finds the IToolDock containing Explorer and toggles visibility
     ToggleSidebarCommand = ReactiveCommand.Create(() =>
     {
         var sidebarDock = FindToolDock(_layout, "Explorer"); // walks VisibleDockables
         if (sidebarDock?.Owner is { } owner)
             owner.IsVisible = !owner.IsVisible;
     });
     
     // ToggleOutputCommand → finds OutputTool and toggles it
     ToggleOutputCommand = ReactiveCommand.Create(() =>
     {
         var output = FindDockable(_layout, "Output");
         if (output is { } dockable)
             dockable.IsVisible = !dockable.IsVisible;
     });
     ```
   - Helper methods `FindToolDock` / `FindDockable` walk the `IRootDock` tree recursively via `IDock.VisibleDockables` / `IDock.Dockables`
2. Update `MainWindow.axaml` View menu items to use new commands
3. Ensure `Ctrl+OemTilde` (toggle output) still works via keybinding

**Deliverable:** All existing keyboard shortcuts and menu items toggle dock panel visibility.

**Test:** Unit test — toggle commands change `IsVisible` on correct dock zones. Manual — Ctrl+` toggles output panel.

---

### M5 — Layout Persistence

**Goal:** Layout state (panel positions, sizes, visibility) persists across restarts.

**Steps:**
1. Create `src/Docking/LayoutPersistenceService.cs`:
   ```csharp
   public interface ILayoutPersistenceService
   {
       void Save(IDock layout);
       IDock? Load();
   }
   
   public class LayoutPersistenceService : ILayoutPersistenceService
   {
       private readonly string _layoutPath;
       
       public LayoutPersistenceService()
       {
           // Cross-platform: Environment.GetFolderPath handles Windows/macOS/Linux
           _layoutPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
               ".aero", "layout.json");
           
           // Create directory on construction (review finding 3.5)
           var dir = Path.GetDirectoryName(_layoutPath);
           if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
               Directory.CreateDirectory(dir);
       }
       
       public void Save(IDock layout)
       {
           var json = DockSerializer<IRootDock>.Serialize((IRootDock)layout);
           // Atomic write: write to temp file then move (prevents corruption on crash)
           var tempPath = _layoutPath + ".tmp";
           File.WriteAllText(tempPath, json);
           File.Move(tempPath, _layoutPath, overwrite: true);
       }
       
       public IDock? Load()
       {
           if (!File.Exists(_layoutPath)) return null;
           try
           {
               var json = File.ReadAllText(_layoutPath);
               return DockSerializer<IRootDock>.Deserialize(json);
           }
           catch
           {
               return null; // corrupted layout → fall back to default
           }
       }
   }
   ```
   > **Note:** All model types (`IRootDock`, `ITool`, `IDocument`, etc.) must be marked with `[DockJsonSerializable]` for serialization to work. The concrete implementations returned by `AeroDockFactory` will carry this attribute.
2. Register in DI: `services.AddSingleton<ILayoutPersistenceService, LayoutPersistenceService>()`
3. In `MainWindow.axaml.cs`:
   - On startup: try `ILayoutPersistenceService.Load()` → if valid and factory-compatible, use restored layout; else use `AeroDockFactory.CreateDefaultLayout()`
   - On close: `ILayoutPersistenceService.Save(DockControl.Layout)`
   - Debounce saves (500ms) to avoid excessive writes during drag
4. Add `~/.aero/` to `.gitignore` (if not already)

**Deliverable:** User rearranges panels → closes IDE → reopens → layout is restored.

**Test:** Integration test — serialize a layout, deserialize it, verify panel IDs and positions match. Manual — rearrange, restart, verify.

---

### M6 — Settings Integration (Layout Mode Stub)

**Goal:** Add `LayoutMode` setting for future Tile Mode (8.1b). 8.1a only uses Freeform.

**Steps:**
1. Use the `LayoutMode` enum from `src/Docking/LayoutMode.cs` (created in M1)
2. Store current mode in `ShellViewModel.LayoutMode` property (default: `LayoutMode.Freeform`)
3. If `8.7-workspace-persistence` is ready, persist via `ISettingsService`; otherwise store in memory only
4. Add UI stub in View menu: "Layout Mode > Freeform" (active, checkmark) with "Tile" option grayed out
5. When 8.1b implements Tile Mode, the mode switch logic swaps the `AeroDockFactory` layout output

**Deliverable:** Settings model has `LayoutMode`. View menu shows the option. No functional change yet.

**Test:** Unit test — `LayoutMode` enum has both values. Manual — View menu shows "Freeform" as active.

---

### M7 — Cleanup and Final Polish

**Goal:** Remove dead code, update documentation, verify all tests pass.

**Steps:**
1. Remove old Grid layout code from `MainWindow.axaml` (M1 replaces it definitively — old code goes into a comment block at bottom for reference, then removed in M7)
2. Remove unused `GridSplitter` references
3. Remove `IsSidebarVisible`, `IsBottomPanelVisible`, `ActiveSidebarTabIndex`, `ActiveBottomTabIndex` from `ShellViewModel` (done in M4)
4. Remove `Dock.Serializer.Newtonsoft` from `aero.csproj` (unused extra dependency — TOFIX R4.2)
5. Verify all DataTemplates are registered and working
6. Update `docs/roadmap/PHASES.md` — mark 8.1a items as complete
7. Update `docs/LIBRARIES.md` — mark Dock.Avalonia as "wired in Phase 8.1a"
8. Run `dotnet build src/aero.csproj` — 0 errors
9. Run `dotnet test tests` — all existing tests pass (527+)
10. Write `manual_test/manual_test_phase8.1a.sh` for manual verification

**Deliverable:** Clean codebase, updated docs, all tests green.

**Test:** `dotnet build` + `dotnet test` both pass. Manual test script covers all scenarios.

---

## 5. Test Expectations

### Unit Tests

| Test | File | Validates |
|------|------|-----------|
| `AeroDockFactoryTests.CreatesDefaultLayout` | `tests/Docking/` | Factory returns valid `IRootDock` with 5 dockables in correct zones |
| `AeroDockFactoryTests.LeftZoneHasTwoTools` | `tests/Docking/` | Explorer + Git are tabs in the left `IToolDock` |
| `AeroDockFactoryTests.BottomZoneHasTwoTools` | `tests/Docking/` | Problems + Output are tabs in the bottom `IToolDock` |
| `AeroDockFactoryTests.CenterHasDocument` | `tests/Docking/` | `IDocumentDock` contains exactly one `IDocument` (Editor) |
| `LayoutPersistenceServiceTests.RoundTrip` | `tests/Docking/` | Serialize → deserialize preserves dockable IDs and structure |
| `LayoutPersistenceServiceTests.MissingFileReturnsNull` | `tests/Docking/` | `Load()` returns null when file doesn't exist |
| `LayoutPersistenceServiceTests.CorruptedFileReturnsNull` | `tests/Docking/` | `Load()` returns null on malformed JSON |
| `ShellViewModelToggleTests` | `tests/ViewModels/` | Toggle commands change `IsVisible` on correct dock tree nodes |

### Integration Tests

| Test | File | Validates |
|------|------|-----------|
| `LayoutSaveRestoreTests.FullCycle` | `tests/Docking/` | Save layout → modify → restore → verify original structure |
| `ModeSwitchTests.FreeformOnly` | `tests/Docking/` | Setting `LayoutMode.Freeform` produces correct layout |

### Manual Tests

| Scenario | Expected Result |
|----------|----------------|
| App starts with default layout | Sidebar (Explorer+Git) on left, Editor center, Bottom (Problems+Output) below |
| Drag Explorer tab to bottom zone | Explorer moves to bottom zone as a tab with Problems/Output |
| Drag Git tab to right side | Right zone created with Git panel |
| Close Explorer panel | Panel disappears; zone collapses if empty |
| Toggle Sidebar via menu | Sidebar zone toggles visibility |
| Toggle Output via Ctrl+` | Output panel toggles visibility |
| Rearrange panels → restart IDE | Layout restored to last arrangement |
| Drag panel onto Editor tab bar | Editor becomes a tab group (if Dock supports it) |

---

## 6. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Dock.Avalonia API changed between docs and 11.3.12.1 | Medium | All types verified against XML docs in NuGet package. `IFactory`, `ITool`, `IDocument`, `IRootDock` all confirmed present. |
| net8.0 TFM fallback causes subtle issues | Low | R1.2 smoke test passed. Monitor for windowing/drag issues |
| `DockObject` does not exist in 11.3.12.1 | High | `DockObject` is a pre-11.x type. M0.5 spike must identify correct base class: `ManagedDockableBase` (confirmed in DLL) or direct `ITool`/`IDocument` impl with `INotifyPropertyChanged`. Removed from M1 examples pending M0.5 verification |
| Concrete model types need `[DockJsonSerializable]` for serialization | Medium | The `Dock.Serializer.SystemTextJson` package requires this attribute. Verify during M5; fallback to manual JSON if needed |
| `IFactory` overrides must return correct concrete types | High | Factory must return types from `Dock.Avalonia` or `Dock.Model` assemblies. No `Dock.Model.Mvvm` package exists. Verify in M0.5 spike which concrete types are public. Fallback: implement `IFactory` directly, let Dock resolve via DataTemplates |
| Existing ViewModel DataContext breaks when wrapped | Medium | DataTemplate approach preserves DataContext on the content. Test each panel individually |
| Layout serialization produces huge JSON | Low | Only serializes dock structure, not content. ~1-5KB typical |
| GridSplitter removal breaks resize behavior | Low | Dock.Avalonia provides its own `IProportionalDockSplitter` between zones |

---

## 7. Dependencies After 8.1a Completes

```
8.1a ──┬── 8.1b (Tile Mode)
        └── 8.1c (Tear-away Windows)
```

8.1a is the foundation. Both 8.1b and 8.1c build on the dock infrastructure established here.

---

## 8. Notes

- **Dock.Avalonia documentation is sparse.** The implementation will rely on GitHub samples at `https://github.com/wieslawsoltes/Dock` and the confirmed API from TOFIX R1.3.
- **The factory is critical.** `DockControl.Layout` requires a `Factory` that creates concrete `IDock`/`IDockable` types. `Dock.Model` provides `FactoryBase` (abstract). No `Dock.Model.Mvvm` package exists — implement `IFactory` directly or extend `FactoryBase`. Use `DockControl.InitializeFactory = true` to wire the factory, but do NOT set `InitializeLayout = true` when providing your own layout (it would overwrite it).
- **DataTemplate registration** is how Dock renders custom content. Register `DataTemplate` in `Window.DataTemplates` mapping each `ITool`/`IDocument` type to its `UserControl`. Dock's `DockControl` uses these templates when rendering. Do NOT use `AutoCreateDataTemplates` — explicit templates are preferred for clarity.
- **Panel header styling** should use 8.9 design tokens (`Radius.Panel`, `Spacing.*`, `Typography.*`) from the start.
- **The status bar stays outside the Dock** — it's always visible at the bottom of the window, not a dockable panel. The expected MainWindow.axaml structure is:
  ```xml
  <DockPanel>
      <Menu DockPanel.Dock="Top"/>
      <StatusBar DockPanel.Dock="Bottom"/>
      <dock:DockControl />  <!-- fills remaining space -->
  </DockPanel>
  ```
- **Menu bar stays outside the Dock** — always at the top, not dockable.
- **EditorView internal tabs** (open files) remain managed by `EditorViewModel` — Dock.Avalonia manages the *panels*, not the tabs within the editor panel.
- **Serialization caveat:** `[DockJsonSerializable]` must be on all model types used in the layout tree. Verify if `FactoryBase`-derived types carry it; if not, custom `ITool`/`IDocument` implementations must carry it.
- **Rollback plan:** If Dock.Avalonia integration fails, keep the Grid layout code behind a feature flag (commented out in M1, restore if M2 fails). Allow switching back via `appsettings.json` layout mode flag.
