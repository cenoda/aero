# 8.1a — Dockable Panels (Freeform Mode): Implementation Plan

> **Status:** Draft — pre-implementation (2026-06-22)
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

### 3.1 Dock.Avalonia Concepts

| Concept | Aero mapping |
|---------|-------------|
| `DockControl` | Root element replacing the Grid layout in `MainWindow.axaml` |
| `Dock` (layout root) | `IRootDock` — the top-level layout container |
| `DockableControl` | Wraps each existing `UserControl` as an `IDockable` |
| `DockLayout` / `DockPanel` | Docking zones (left, right, center, bottom) |
| `ILayoutDockable` | Panel metadata (title, icon, close behavior) |
| `DockSerializer<T>` | JSON serialization for layout persistence |

### 3.2 Layout Model

```
RootDock
├── LeftDock (sidebar zone)
│   ├── FileExplorerDockable
│   └── GitDockable
├── CenterDock (editor zone)
│   └── EditorDockable
└── BottomDock (bottom zone)
    ├── ProblemsDockable
    └── OutputDockable
```

### 3.3 New Files to Create

| File | Purpose |
|------|---------|
| `src/Docking/DockPanelFactory.cs` | Creates the initial `IRootDock` layout from panel ViewModels |
| `src/Docking/LayoutPersistenceService.cs` | Save/restore `IRootDock` to `~/.aero/layout.json` |
| `src/Docking/LayoutMode.cs` | `enum LayoutMode { Freeform, Tile }` |
| `src/Docking/PanelDescriptors.cs` | Metadata for each dockable panel (title, icon, default zone) |
| `src/Docking/DockableContentTemplate.axaml` | Generic DataTemplate wrapping ViewModels in Dock panels |

### 3.4 Files to Modify

| File | Changes |
|------|---------|
| `src/MainWindow.axaml` | Replace Grid layout with `DockControl` |
| `src/MainWindow.axaml.cs` | Initialize dock controller, wire layout persistence |
| `src/ViewModels/ShellViewModel.cs` | Remove panel-visibility booleans; add `DockController` property; wire toggle commands to dock hide/show |
| `src/App.axaml.cs` | Register `LayoutPersistenceService` in DI |
| `src/Services/DocumentManager.cs` | No changes (editor tabs are internal to `EditorViewModel`) |

---

## 4. Milestones

### M1 — Dock Infrastructure Skeleton

**Goal:** Replace the Grid layout with a `DockControl` that renders a dockable editor area.

**Steps:**
1. Create `src/Docking/PanelDescriptors.cs` — metadata record for each panel:
   ```csharp
   public record PanelDescriptor(string Id, string Title, string IconKey, DockPosition DefaultPosition);
   public enum DockPosition { Left, Center, Bottom, Right, Floating }
   ```
2. Create `src/Docking/DockPanelFactory.cs` — builds initial `IRootDock` tree:
   - Takes 5 panel `UserControl` instances (injected by MainWindow)
   - Returns a pre-configured `RootDock` with left/center/bottom zones
   - Uses Dock.Avalonia's `ProportionalDock` for zone sizing
3. Update `src/MainWindow.axaml`:
   - Remove the Grid (columns 0-2) and GridSplitters
   - Add `<DockControl x:Name="DockControl" />` as the content
4. Update `src/MainWindow.axaml.cs`:
   - In `OnInitialized`, call `DockPanelFactory.CreateLayout()`
   - Assign result to `DockControl.Layout`

**Deliverable:** IDE starts with a docked layout. Editor fills center. Sidebar and bottom panel are dockable zones but may not yet contain panels.

**Test:** Manual — app starts, no crash, `DockControl` renders.

---

### M2 — Wrap Existing Panels as Dockables

**Goal:** All 5 existing panels render inside Dock.Avalonia dockable containers.

**Steps:**
1. Create `src/Docking/DockableContentTemplate.axaml`:
   - Generic `DataTemplate` that wraps a ViewModel's `UserControl` inside a `DockControl` panel
   - Template uses `ContentPresenter` with `Content="{Binding}"` to host any ViewModel
2. For each panel ViewModel, create a lightweight `DockableWrapper` class:
   ```csharp
   public class DockableWrapper : DockableBase
   {
       public string Title { get; set; }
       public string IconKey { get; set; }
       public UserControl Content { get; set; }
   }
   ```
   - `DockableBase` is Dock.Avalonia's base class implementing `IDockable`
   - `Content` holds the existing `UserControl` instance
3. Update `DockPanelFactory` to wrap each panel:
   - `FileExplorerViewModel` → `DockableWrapper("Explorer", "Icon.Folder", FileExplorerView)`
   - `GitViewModel` → `DockableWrapper("Git", "Icon.Code", GitPanelView)`
   - `EditorViewModel` → `DockableWrapper("Editor", "Icon.Code", EditorView)`
   - `ProblemsViewModel` → `DockableWrapper("Problems", "Icon.Text", ProblemsView)`
   - `OutputViewModel` → `DockableWrapper("Output", "Icon.Text", OutputView)`
4. Configure zone tabs:
   - Left zone: `ProportionalDock` with `Explorer` and `Git` as tabs
   - Bottom zone: `ProportionalDock` with `Problems` and `Output` as tabs
   - Center zone: `Editor` (sole content)

**Deliverable:** All 5 panels visible in their default positions. Sidebar has Explorer+Git tabs. Bottom has Problems+Output tabs. Editor fills center.

**Test:** Manual — all panels render with correct content. Tabs switch between Explorer/Git and Problems/Output.

---

### M3 — Drag-and-Drop Rearrangement

**Goal:** User can drag panels between docking zones (left ↔ center ↔ bottom ↔ right).

**Steps:**
1. Verify Dock.Avalonia's built-in drag-and-drop works with our `DockableWrapper` panels:
   - Drag from left zone to bottom zone → panel moves
   - Drag from bottom zone to left zone → panel moves
   - Drag from center zone → creates floating panel (if supported)
2. Ensure `DockControl` is configured with `EnableDragDrop="True"` (or equivalent property)
3. Add visual feedback:
   - Dock highlight rectangles appear when dragging
   - Drop zones light up on hover
4. Handle edge cases:
   - Drag last panel out of a zone → zone collapses
   - Drag panel back into collapsed zone → zone re-expands
   - Tab grouping: drag panel onto another panel's tab bar → creates tab group

**Deliverable:** All 5 panels can be freely rearranged by dragging.

**Test:** Manual — drag each panel to every other zone. Verify no crashes, visual feedback appears, tabs group correctly.

---

### M4 — Panel Visibility Toggle Commands

**Goal:** Existing View menu commands (Toggle Sidebar, Toggle Output, etc.) work with Dock layout.

**Steps:**
1. Update `ShellViewModel`:
   - Remove `IsSidebarVisible`, `ActiveSidebarTabIndex`, `IsBottomPanelVisible`, `ActiveBottomTabIndex`
   - Add `IDockController DockController` property (injected or set by MainWindow)
   - Rewrite toggle commands:
     ```csharp
     // ToggleSidebarCommand → hides/shows the left dock zone
     ToggleSidebarCommand = ReactiveCommand.Create(() =>
     {
         var leftDock = DockController.FindLayout("left");
         if (leftDock != null) leftDock.IsVisible = !leftDock.IsVisible;
     });
     ```
   - Similarly for `ToggleOutputCommand`, `ToggleProblemsCommand`, `ToggleBottomPanelCommand`
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
   public class LayoutPersistenceService : ILayoutPersistenceService
   {
       private readonly string _layoutPath;
       
       public LayoutPersistenceService()
       {
           _layoutPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
               ".aero", "layout.json");
       }
       
       public void Save(IRootDock layout)
       {
           var json = DockSerializer<RootDock>.Serialize((RootDock)layout);
           File.WriteAllText(_layoutPath, json);
       }
       
       public IRootDock? Load()
       {
           if (!File.Exists(_layoutPath)) return null;
           var json = File.ReadAllText(_layoutPath);
           return DockSerializer<RootDock>.Deserialize(json);
       }
   }
   ```
2. Register in DI: `services.AddSingleton<ILayoutPersistenceService, LayoutPersistenceService>()`
3. In `MainWindow.axaml.cs`:
   - On startup: try `Load()` → if valid, use restored layout; else use `DockPanelFactory.CreateLayout()`
   - On close: `Save(DockControl.Layout)`
   - Debounce saves (500ms) to avoid excessive writes during drag
4. Add `~/.aero/` to `.gitignore` (if not already)

**Deliverable:** User rearranges panels → closes IDE → reopens → layout is restored.

**Test:** Integration test — serialize a layout, deserialize it, verify panel IDs and positions match. Manual — rearrange, restart, verify.

---

### M6 — Settings Integration (Layout Mode Stub)

**Goal:** Add `LayoutMode` setting for future Tile Mode (8.1b). 8.1a only uses Freeform.

**Steps:**
1. Add `LayoutMode` enum to `src/Docking/`:
   ```csharp
   public enum LayoutMode { Freeform, Tile }
   ```
2. Add `LayoutMode` to settings model (if `8.7-workspace-persistence` is ready) or store in `ShellViewModel` for now
3. Add UI stub in View menu: "Layout Mode > Freeform" (disabled, grayed out) with "Tile" option grayed out
4. When 8.1b implements Tile Mode, the mode switch logic swaps the `DockPanelFactory` output

**Deliverable:** Settings model has `LayoutMode`. View menu shows the option. No functional change yet.

**Test:** Unit test — `LayoutMode` enum has both values. Manual — View menu shows "Freeform" as active.

---

### M7 — Cleanup and Final Polish

**Goal:** Remove dead code, update documentation, verify all tests pass.

**Steps:**
1. Remove old Grid layout code from `MainWindow.axaml` (if not already removed in M1)
2. Remove unused `GridSplitter` references
3. Remove `IsSidebarVisible`, `IsBottomPanelVisible` from `ShellViewModel` (if not done in M4)
4. Update `docs/roadmap/PHASES.md` — mark 8.1a items as complete
5. Update `docs/LIBRARIES.md` — mark Dock.Avalonia as "wired in Phase 8.1a"
6. Run `dotnet build src/aero.csproj` — 0 errors
7. Run `dotnet test tests` — all existing tests pass
8. Write `manual_test/manual_test_phase8.1a.sh` for manual verification

**Deliverable:** Clean codebase, updated docs, all tests green.

**Test:** `dotnet build` + `dotnet test` both pass. Manual test script covers all scenarios.

---

## 5. Test Expectations

### Unit Tests

| Test | File | Validates |
|------|------|-----------|
| `DockPanelFactoryTests.CreatesDefaultLayout` | `tests/Docking/` | Factory returns valid `IRootDock` with 5 panels in correct zones |
| `DockPanelFactoryTests.LeftZoneHasTwoTabs` | `tests/Docking/` | Explorer + Git are tabs in left zone |
| `DockPanelFactoryTests.BottomZoneHasTwoTabs` | `tests/Docking/` | Problems + Output are tabs in bottom zone |
| `LayoutPersistenceServiceTests.RoundTrip` | `tests/Docking/` | Serialize → deserialize preserves panel IDs and positions |
| `LayoutPersistenceServiceTests.MissingFileReturnsNull` | `tests/Docking/` | `Load()` returns null when file doesn't exist |
| `ShellViewModelToggleTests` | `tests/ViewModels/` | Toggle commands change `IsVisible` on correct dock zones |

### Integration Tests

| Test | File | Validates |
|------|------|-----------|
| `LayoutSaveRestoreTests.FullCycle` | `tests/Docking/` | Save layout → modify → restore → verify original state |
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
| Dock.Avalonia API changed between docs and 11.3.12.1 | Medium | R1.3 confirmed API exists. Fallback: manual layout with Grid + splitter (current approach, acceptable) |
| net8.0 TFM fallback causes subtle issues | Low | R1.2 smoke test passed. Monitor for windowing/drag issues |
| Existing ViewModel DataContext breaks when wrapped | Medium | `DockableWrapper` preserves `DataContext` on the inner `UserControl`. Test each panel individually |
| Layout serialization produces huge JSON | Low | Only serializes dock structure, not content. ~1-5KB typical |
| GridSplitter removal breaks resize behavior | Low | Dock.Avalonia provides its own splitters between zones |

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
- **Panel header styling** should use 8.9 design tokens (`Radius.Panel`, `Spacing.*`, `Typography.*`) from the start.
- **The status bar stays outside the Dock** — it's always visible at the bottom of the window, not a dockable panel.
- **Menu bar stays outside the Dock** — always at the top, not dockable.
- **EditorView internal tabs** (open files) remain managed by `EditorViewModel` — Dock.Avalonia manages the *panels*, not the tabs within the editor panel.
