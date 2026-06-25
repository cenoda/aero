# Phase 8.1 — Panel Polish & Layout Refinement

**Status:** Ready for implementation  
**Goal:** Transform the existing Grid+GridSplitter layout into a polished, professional IDE shell.  
**Aesthetic target:** JetBrains Rider — clean surfaces, subtle depth, blue accent indicators.  
**Constraint:** No Dock.Avalonia. Fixed Grid+GridSplitter layout, polished to perfection.

---

## Entry Gates (M0)

- [ ] `dotnet build src/aero.csproj` passes (0 errors)
- [ ] `dotnet test tests` passes (baseline: 525 passed)
- [ ] Phase 8.9 design system tokens are present in `src/Styles/ThemeLight.axaml`
- [ ] `src/Styles/Icons.axaml` contains Phosphor icons (Folder, Code, etc.)
- [ ] Phase 8.7 `ISettingsService` exists at `src/Services/SettingsService.cs`

---

## Current Baseline (verified 2026-06-25)

```
MainWindow.axaml
├── DockPanel
│   ├── Menu (Top)
│   └── Grid (3 columns: sidebar | splitter | editor+bottom)
│       ├── Col 0: Grid (IsVisible={IsSidebarVisible})
│       │   └── TabControl (Explorer | Git)         ← unstyled, no header
│       ├── Col 1: GridSplitter (4px, panel.border)  ← no hover state
│       └── Col 2: Grid (3 rows)
│           ├── Row 0: EditorView
│           ├── Row 1: GridSplitter (4px, panel.border) ← no hover state
│           └── Row 2: TabControl (Problems | Output)   ← unstyled, MinHeight=100
│               └── Row 3: Border (status bar)
```

**Design tokens already in ThemeLight.axaml:**
`panel.headerBackground`, `panel.headerForeground`, `panel.border`, `panel.background`,
`panel.hoverBackground`, `panel.emptyStateForeground`, `splitter.background`,
`splitter.hoverBackground`, `tab.activeBackground`, `tab.activeForeground`,
`tab.activeBorderTop`, `tab.inactiveBackground`, `tab.inactiveForeground`

**Structural tokens in CornerRadius.axaml:** `Radius.Panel` (8px), `Radius.Tab` (4px)  
**Timing in Transitions.axaml:** `Transition.Duration` (200ms)

---

## Milestones

### M1 — Panel Headers & Visual Hierarchy

**Goal:** Polished header bars on every panel. Think Rider's tool window title bars.

#### M1.1: Sidebar Header

Add a header bar above the sidebar `TabControl`:

```xml
<!-- Header sits above the TabControl in a DockPanel -->
<DockPanel>
    <Border DockPanel.Dock="Top"
            Background="{DynamicResource panel.headerBackground}"
            BorderBrush="{DynamicResource panel.border}"
            BorderThickness="0,0,0,1"
            Padding="12,6">
        <Grid ColumnDefinitions="Auto,*,Auto">
            <!-- Icon -->
            <PathIcon Grid.Column="0"
                      Data="{StaticResource Icon.Folder}"
                      Width="14" Height="14"
                      Foreground="{DynamicResource panel.headerForeground}"
                      Margin="0,0,6,0"/>
            <!-- Title (shows active tab name: Explorer / Git) -->
            <TextBlock Grid.Column="1"
                       Text="{Binding ActiveSidebarTitle}"
                       Foreground="{DynamicResource panel.headerForeground}"
                       FontWeight="SemiBold"
                       FontSize="11"
                       VerticalAlignment="Center"/>
            <!-- Collapse button -->
            <Button Grid.Column="2"
                    Command="{Binding ToggleSidebarCommand}"
                    Background="Transparent"
                    BorderThickness="0"
                    Padding="4"
                    ToolTip.Tip="Collapse sidebar">
                <PathIcon Data="{StaticResource Icon.ChevronLeft}"
                          Width="12" Height="12"
                          Foreground="{DynamicResource panel.headerForeground}"/>
            </Button>
        </Grid>
    </Border>
    <TabControl SelectedIndex="{Binding ActiveSidebarTabIndex}" .../>
</DockPanel>
```

**Icons needed:** `Icon.ChevronLeft` (add to `Icons.axaml`)  
**ViewModel change:** Add `ActiveSidebarTitle` computed property to `ShellViewModel`.

#### M1.2: Bottom Panel Header

Mirror M1.1 for the bottom `TabControl`. Header shows active tab name (Problems / Output).

```xml
<Border Background="{DynamicResource panel.headerBackground}"
        BorderBrush="{DynamicResource panel.border}"
        BorderThickness="0,1,0,0"
        Padding="12,4">
    <Grid ColumnDefinitions="Auto,*,Auto">
        <TextBlock Grid.Column="1"
                   Text="{Binding ActiveBottomTabTitle}"
                   Foreground="{DynamicResource panel.headerForeground}"
                   FontWeight="SemiBold" FontSize="11"/>
        <Button Grid.Column="2"
                Command="{Binding ToggleBottomPanelCommand}"
                Background="Transparent" BorderThickness="0" Padding="4"
                ToolTip.Tip="Collapse panel">
            <PathIcon Data="{StaticResource Icon.ChevronDown}"
                      Width="12" Height="12"/>
        </Button>
    </Grid>
</Border>
```

**Icons needed:** `Icon.ChevronDown` (add to `Icons.axaml`)  
**ViewModel change:** Add `ActiveBottomTabTitle` computed property.

#### M1.3: Header Action Buttons

- Hover state: `Background="{DynamicResource panel.hoverBackground}"` via `ControlTheme`
  or inline `Style Selector`
- Cursor: `Cursor="Hand"`
- 150ms opacity transition on hover (via `Transitions` in style)

**Test M1:** All panels show a dark header bar with icon, title, and a working collapse button.

---

### M2 — Border & Spacing Refinement

**Goal:** Clean 1px borders, consistent spacing, no raw edges.

#### M2.1: Panel Container Borders

Sidebar and bottom panel panels should be wrapped to give them defined edges.

- Apply `CornerRadius` to the sidebar container's top corners only:
  ```xml
  <Border CornerRadius="8,8,0,0"
          BorderBrush="{DynamicResource panel.border}"
          BorderThickness="1">
  ```
- The editor panel has no border by design (it is the primary content area).
- Status bar: already uses `panel.sectionBackground` + `panel.border` top edge — verify
  it matches `panel.sectionBackground` in both themes.

#### M2.2: Tab Strip Spacing

Current `TabItem` padding comes from `ControlThemes.axaml` (`Tab.PaddingThickness`).
Verify it gives 12px horizontal / 6px vertical — matches Rider's compact tab feel.

#### M2.3: Internal Padding

- File tree items: `Padding="4,2"` per row (already set in `FileExplorerView.axaml` — verify)
- Bottom panel content area: `Padding="8"` inside Problems and Output views
- Splitter: no padding needed — 4px hit area is sufficient

**Test M2:** No raw edges. All panels have consistent 1px borders. Status bar edge matches.

---

### M3 — Collapse/Expand Animations

**Goal:** Smooth 200ms transitions — zero layout jump, zero flicker.

**Avalonia constraint:** Animating `Width`/`Height` on `ColumnDefinition`/`RowDefinition`
is not directly supported via `Transitions`. The correct approach:

- **Sidebar collapse:** Set `ColumnDefinition.Width` to `0` and use a `GridLength`
  animation via code-behind, or alternatively set `Width` on the sidebar's wrapping
  `Grid` control using a `Transitions` collection on the `Grid`:
  ```xml
  <Grid.Transitions>
      <Transitions>
          <DoubleTransition Property="Width"
                            Duration="0:0:0.200"
                            Easing="CubicEaseOut"/>
      </Transitions>
  </Grid.Transitions>
  ```
  Bind `Width` reactively from `ShellViewModel` (`SidebarWidth` property: `0.0` or `250.0`).

- **Bottom panel collapse:** Same technique. `BottomPanelHeight` property: `0.0` or `150.0`.
  The `TabControl` in Row 2 binds `Height` and transitions smoothly.

- **GridSplitter visibility:** Hide the splitter (`IsVisible`) when the adjacent panel
  collapses, synced via a computed binding.

#### M3.1: Sidebar Width Animation

- Add `[Reactive] public double SidebarWidth { get; set; } = 250;` to `ShellViewModel`
- `ToggleSidebar()`: set `SidebarWidth = IsSidebarVisible ? 250 : 0`
- Bind `Grid.Width="{Binding SidebarWidth}"` on the sidebar wrapper
- Add `DoubleTransition` for `Width` (200ms, CubicEaseOut) in XAML
- Keep `IsSidebarVisible` for the splitter's `IsVisible` binding (use `SidebarWidth > 0`)

#### M3.2: Bottom Panel Height Animation

- Add `[Reactive] public double BottomPanelHeight { get; set; } = 150;`
- `ToggleBottomPanel()`: set `BottomPanelHeight = IsBottomPanelVisible ? 150 : 0`
- Bind `TabControl.Height="{Binding BottomPanelHeight}"`
- Add `DoubleTransition` for `Height` (200ms, CubicEaseOut)

#### M3.3: Animation Coordination

- Editor expands naturally when sidebar closes (Grid star-sizing handles this)
- GridSplitter `IsVisible="{Binding SidebarWidth, Converter={...IsGreaterThanZero}}"` — or
  use `IsSidebarVisible` directly (simpler: hides at end of animation)
- No flicker: transitions run on the UI thread — Avalonia renders each frame smoothly

**Test M3:** Toggle sidebar (Ctrl+Shift+E or View menu) and bottom panel — 200ms smooth
animation, editor fills the freed space, no jump.

---

### M4 — Tab Strip Styling

**Goal:** Active tab has a blue underline indicator. Inactive tabs are muted. Hover is visible.

This requires a custom `ControlTheme` for `TabItem` in `ControlThemes.axaml`.

#### M4.1: TabItem ControlTheme

```xml
<ControlTheme x:Key="{x:Type TabItem}" TargetType="TabItem">
    <Setter Property="Background" Value="{DynamicResource tab.inactiveBackground}"/>
    <Setter Property="Foreground" Value="{DynamicResource tab.inactiveForeground}"/>
    <Setter Property="Padding" Value="12,6"/>
    <Setter Property="Template">
        <ControlTemplate>
            <Border Name="PART_Root"
                    Background="{TemplateBinding Background}"
                    BorderBrush="Transparent"
                    BorderThickness="0,0,0,2"
                    Padding="{TemplateBinding Padding}">
                <ContentPresenter Content="{TemplateBinding Header}"/>
            </Border>
        </ControlTemplate>
    </Setter>
    <!-- Active state -->
    <Style Selector="^:selected">
        <Setter Property="Background" Value="{DynamicResource tab.activeBackground}"/>
        <Setter Property="Foreground" Value="{DynamicResource tab.activeForeground}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <!-- Blue underline via Border in template -->
    </Style>
    <!-- Hover state -->
    <Style Selector="^:pointerover">
        <Setter Property="Background" Value="{DynamicResource panel.hoverBackground}"/>
    </Style>
</ControlTheme>
```

The blue underline on the active tab comes from the `Border`'s `BorderBrush`:
```xml
<!-- In :selected pseudo-class, target the inner PART_Root -->
<Style Selector="^:selected /template/ Border#PART_Root">
    <Setter Property="BorderBrush" Value="{DynamicResource tab.activeBorderTop}"/>
</Style>
```

#### M4.2: TabControl Background

```xml
<Style Selector="TabControl">
    <Setter Property="Background" Value="{DynamicResource tab.background}"/>
</Style>
```

**Test M4:** Click between Explorer / Git tabs and Problems / Output tabs — active tab has
a blue 2px bottom border, inactive tabs are muted.

---

### M5 — Empty States

**Goal:** Every empty panel has a centered, helpful message with a Phosphor icon.
No blank or raw views.

#### M5.1: File Explorer Empty

When `FileExplorerViewModel.RootNodes` is empty (no folder open):

```xml
<Panel>
    <!-- Tree (shown when folder open) -->
    <TreeView IsVisible="{Binding HasFolder}" .../>
    <!-- Empty state (shown when no folder) -->
    <StackPanel IsVisible="{Binding !HasFolder}"
                HorizontalAlignment="Center"
                VerticalAlignment="Center"
                Spacing="8">
        <PathIcon Data="{StaticResource Icon.Folder}"
                  Width="40" Height="40"
                  Foreground="{DynamicResource panel.emptyStateForeground}"
                  HorizontalAlignment="Center"/>
        <TextBlock Text="No folder opened"
                   Foreground="{DynamicResource panel.emptyStateForeground}"
                   FontWeight="SemiBold"
                   HorizontalAlignment="Center"/>
        <TextBlock Text="Open a folder to get started"
                   Foreground="{DynamicResource panel.emptyStateForeground}"
                   FontSize="11"
                   HorizontalAlignment="Center"/>
        <Button Content="Open Folder"
                Command="{Binding $parent[Window].DataContext.OpenFolderCommand}"
                HorizontalAlignment="Center"
                Margin="0,4,0,0"/>
    </StackPanel>
</Panel>
```

**ViewModel change:** Add `bool HasFolder` to `FileExplorerViewModel` (already partially
exists — verify `RootNodes.Count > 0`).

#### M5.2: Problems Empty

When `ProblemsViewModel.Problems` is empty:
- Icon: checkmark circle (green tint)
- Title: "No problems detected"
- Subtitle: "Errors and warnings will appear here"

**Icon needed:** `Icon.CheckCircle` (add to `Icons.axaml`)

#### M5.3: Output Empty

When `OutputViewModel.Lines` is empty (no build run yet):
- Icon: `Icon.Code` (terminal-like)
- Title: "No output yet"
- Subtitle: "Run a build or command to see output"

#### M5.4: Git Panel Empty

When `GitViewModel.HasGitRepository` is false:
- Icon: `Icon.GitBranch` (add to `Icons.axaml`)
- Title: "No repository"
- Subtitle: "Open a folder with a Git repository"

**Test M5:** Open the IDE with no folder — Explorer shows centered empty state with icon and
"Open Folder" button. Close the folder — state returns to empty. Problems/Output/Git panels
each have their own appropriate empty state.

---

### M6 — GridSplitter Hover Polish

**Goal:** Splitters give visual feedback — blue highlight on hover, correct resize cursor.

#### M6.1: Splitter ControlTheme

```xml
<ControlTheme x:Key="{x:Type GridSplitter}" TargetType="GridSplitter">
    <Setter Property="Background" Value="{DynamicResource splitter.background}"/>
    <Style Selector="^:pointerover">
        <Setter Property="Background" Value="{DynamicResource splitter.hoverBackground}"/>
    </Style>
</ControlTheme>
```

Avalonia's `GridSplitter` exposes `Background` directly — no `ControlTemplate` needed.
The 200ms transition can be added via:
```xml
<Setter Property="Transitions">
    <Transitions>
        <BrushTransition Property="Background"
                         Duration="{StaticResource Transition.Duration}"/>
    </Transitions>
</Setter>
```

#### M6.2: Min-Size Constraints

- Sidebar `ColumnDefinition MinWidth="0"` — already set; keep as-is (allows full collapse)
- Editor `ColumnDefinition MinWidth="200"` — already set; verify
- Bottom panel `MinHeight="80"` (user should not be able to drag it fully away via splitter;
  use `ToggleBottomPanelCommand` for that)

**Test M6:** Hover over both splitters — background transitions from `splitter.background`
(light grey) to `splitter.hoverBackground` (blue accent) in 200ms. Cursor changes to
resize arrows.

---

### M7 — Panel State Persistence

**Goal:** Sidebar and bottom panel visibility / size remembered across restarts.

#### M7.1: Settings Model Extension

Add to `SettingsModel` in `src/Models/Settings/SettingsModel.cs`:

```csharp
public record SettingsModel
{
    // ... existing properties ...
    public bool IsSidebarVisible { get; init; } = true;
    public bool IsBottomPanelVisible { get; init; } = true;
    public double SidebarWidth { get; init; } = 250;
    public double BottomPanelHeight { get; init; } = 150;
}
```

#### M7.2: Load on Startup

In `ShellViewModel.InitializeAsync()` (or equivalent startup path), after loading settings:
```csharp
IsSidebarVisible = settings.IsSidebarVisible;
IsBottomPanelVisible = settings.IsBottomPanelVisible;
SidebarWidth = settings.IsSidebarVisible ? settings.SidebarWidth : 0;
BottomPanelHeight = settings.IsBottomPanelVisible ? settings.BottomPanelHeight : 0;
```

#### M7.3: Save on Toggle and Close

- In `ToggleSidebar()` / `ToggleBottomPanel()` — save settings after mutating state
- In `SaveWorkspaceStateAsync()` — already called on window close (R3.1 fix); extend it
  to also persist panel state:
  ```csharp
  await _settingsService.SaveSettingsAsync(currentSettings with
  {
      IsSidebarVisible = IsSidebarVisible,
      IsBottomPanelVisible = IsBottomPanelVisible,
      SidebarWidth = SidebarWidth > 0 ? SidebarWidth : 250,
      BottomPanelHeight = BottomPanelHeight > 0 ? BottomPanelHeight : 150,
  });
  ```

**Test M7:** Collapse sidebar → close app → reopen → sidebar is still collapsed. Resize
sidebar to 350px → close → reopen → sidebar is 350px.

---

## Implementation Order

Implement strictly one milestone at a time. Verify `dotnet build` passes after each.
Run `dotnet test tests` after M4 (tab templates can cause XAML compile errors).

```
M6 (Splitter hover)   ← smallest change, easiest win, confirms ControlTheme works
M4 (Tab styling)      ← ControlTheme for TabItem (verifies XAML theming approach)
M1 (Panel headers)    ← requires icon additions + ViewModel computed properties
M2 (Border & spacing) ← XAML-only, safe
M3 (Animations)       ← ViewModel + XAML transitions (most likely to surprise)
M5 (Empty states)     ← view-layer only, needs icon additions
M7 (Persistence)      ← model + service changes
```

---

## Files to Change

| File | Changes |
|------|---------|
| `src/Styles/ControlThemes.axaml` | Add `GridSplitter` ControlTheme (M6), `TabItem` ControlTheme (M4), `TabControl` style (M4) |
| `src/Styles/Icons.axaml` | Add `Icon.ChevronLeft`, `Icon.ChevronDown`, `Icon.CheckCircle`, `Icon.GitBranch` |
| `src/MainWindow.axaml` | Wrap sidebar + bottom panel with header bars (M1), add `DoubleTransition` bindings (M3), border tweaks (M2) |
| `src/ViewModels/ShellViewModel.cs` | Add `SidebarWidth`, `BottomPanelHeight`, `ActiveSidebarTitle`, `ActiveBottomTabTitle` (M1, M3), persist state (M7) |
| `src/Models/Settings/SettingsModel.cs` | Add 4 panel-state properties (M7) |
| `src/Views/FileExplorerView.axaml` | Add empty state (M5) |
| `src/Views/ProblemsView.axaml` | Add/verify empty state (M5) |
| `src/Views/OutputView.axaml` | Add empty state (M5) |
| `src/Views/GitPanelView.axaml` | Add empty state (M5) |

---

## Design Token Reference

All tokens already defined in `ThemeLight.axaml` and `ThemeDark.axaml`.

| Token | Where Used |
|-------|-----------|
| `panel.headerBackground` | M1 header bars |
| `panel.headerForeground` | M1 icon + title |
| `panel.border` | M2 borders, header bottom edge |
| `panel.hoverBackground` | M1 action buttons hover, M4 tab hover |
| `panel.emptyStateForeground` | M5 empty state icons + text |
| `splitter.background` | M6 default splitter |
| `splitter.hoverBackground` | M6 hover state |
| `tab.background` | M4 tab strip background |
| `tab.activeBackground` | M4 active tab fill |
| `tab.activeForeground` | M4 active tab text |
| `tab.activeBorderTop` | M4 blue underline indicator |
| `tab.inactiveBackground` | M4 inactive tab fill |
| `tab.inactiveForeground` | M4 inactive tab text |
| `Radius.Panel` (8px) | M2 panel container corners |
| `Transition.Duration` (200ms) | M3 animations, M6 splitter hover |

---

## Exit Criteria (Definition of Done)

| Milestone | Verifiable Condition |
|-----------|---------------------|
| M1 | Sidebar and bottom panel each have a header bar with title text and a functioning collapse button |
| M2 | All panel containers have 1px `panel.border` edges; no raw unstyled edges visible |
| M3 | Toggle sidebar and bottom panel — smooth 200ms transition, editor fills space, no jump or flicker |
| M4 | Clicking between tabs shows 2px blue bottom border on active tab; inactive tabs are muted |
| M5 | Open IDE with no folder: Explorer shows centered empty state with Folder icon and "Open Folder" button |
| M6 | Hover over vertical and horizontal splitters — background transitions to blue accent in ~200ms |
| M7 | Collapse sidebar → quit → relaunch → sidebar remains collapsed |

**Phase complete when:** All 7 rows above pass manual verification, `dotnet build` 0 errors,
`dotnet test tests` ≥ 525 passed.

---

## Phase 8.1 Limitations (by design)

- **No drag-to-rearrange** — panels are fixed to sidebar-left + editor-center + bottom layout.
  Tear-away windows and freeform docking are not part of this phase.
- **No per-panel resize animation** — width/height animate on toggle only.
  The user can still drag the GridSplitter freely at any time.
- **Tab order is fixed** — Explorer/Git in sidebar, Problems/Output in bottom.
  Drag-to-reorder tabs is deferred to Phase 9.
- **Single sidebar** — one sidebar zone (left). A right sidebar is not part of this phase.

---

## Manual Test Script

Create `manual_test/manual_test_phase8.1.sh` at the end of implementation.

Steps to cover:
1. Launch with no folder open — verify all 4 empty states
2. Toggle sidebar (View → Toggle Sidebar) — verify 200ms animation + collapse button
3. Toggle bottom panel (View → Toggle Bottom Panel / Ctrl+`) — verify same
4. Hover both splitters — verify blue highlight
5. Click between tabs in sidebar and bottom panel — verify active indicator
6. Resize sidebar to ~350px via drag — close app — reopen — verify width restored
7. Toggle Dark theme — verify all panel tokens look correct in dark mode

---

## References

- Design tokens: `src/Styles/ThemeLight.axaml`, `ThemeDark.axaml`
- Structural tokens: `src/Styles/CornerRadius.axaml`, `Transitions.axaml`, `Spacing.axaml`
- Icons: `src/Styles/Icons.axaml`
- Settings model: `src/Models/Settings/SettingsModel.cs`
- Settings service: `src/Services/SettingsService.cs`
- Shell logic: `src/ViewModels/ShellViewModel.cs`
