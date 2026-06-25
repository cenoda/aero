# 8.1 — Panel Polish & Layout Refinement: Implementation Plan

> **Status:** Draft — direction changed (2026-06-25)
> **Author:** Kiro (via GitHub Copilot)
> **Parent:** [`README.md`](./README.md)
> **Depends on:** 8.9 Design System ✅, 8.5 Icon Decision ✅, 8.7 Workspace Persistence ✅

---

## 0. Direction Change (2026-06-25)

Dock.Avalonia integration failed twice (v1 on `failed-dockable-panels`, v2 on `phase-8.1a-dockable-panels-v2` → preserved as `failed-dockable-panels-v3`). The library's internal rendering is opaque — code can be correct but the UI doesn't render as expected. After 13+ debug cycles per attempt, the cost of debugging exceeds the value of the feature.

**New direction:** Polish the existing Grid+GridSplitter layout. The sidebar+editor+bottom-panel layout is what 95% of users actually use. Make it beautiful instead of making it draggable.

**What we keep:** The existing `MainWindow.axaml` layout — sidebar (Explorer+Git tabs), editor center, bottom panel (Problems+Output tabs), GridSplitters, status bar. All working. All tested.

**What we add:** Visual polish — headers, borders, animations, empty states, splitter hover effects.

---

## 1. Current State

### Layout (already working, no structural changes)

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

### Design tokens available (from 8.9)

| Token | Value | Usage |
|-------|-------|-------|
| `panel.background` | Theme-dependent | Panel body background |
| `panel.border` | Theme-dependent | 1px panel borders |
| `panel.headerBackground` | Theme-dependent | Panel header bar |
| `panel.sectionBackground` | Theme-dependent | Section headers within panels |
| `panel.foreground` | Theme-dependent | Panel text |
| `panel.sectionForeground` | Theme-dependent | Section header text |
| `spacing.panelPadding` | 8px | Inner panel padding |
| `spacing.headerPadding` | 8,6 | Header bar padding |
| `radius.panel` | 8px | Panel corner radius |
| `transition.default` | 200ms CubicOut | All animations |

---

## 2. Scope

### M1: Panel Headers

- [ ] Add consistent header bar to each panel (Explorer, Git, Problems, Output)
- [ ] Header contains: Phosphor icon (16px) + title text (SemiBold, 12px) + optional action buttons
- [ ] Header uses `panel.headerBackground` + `panel.border` (bottom border only)
- [ ] Header height: 32px (matches design system spacing scale)

### M2: Panel Borders & Spacing

- [ ] Apply `panel.border` to panel outer edges consistently
- [ ] Apply `radius.panel` (8px) to panel containers where appropriate
- [ ] Ensure `spacing.panelPadding` (8px) is consistent across all panels
- [ ] Remove any hardcoded colors — all must use DynamicResource

### M3: Collapse/Expand Animations

- [ ] Sidebar toggle: 200ms width transition (250px ↔ 0) with CubicOut easing
- [ ] Bottom panel toggle: 200ms height transition (150px ↔ 0) with CubicOut easing
- [ ] Content fades out before collapse, fades in after expand
- [ ] No layout jump during animation

### M4: Tab Strip Polish

- [ ] Tab headers in sidebar TabControl: 28px height, 12px font, icon + text
- [ ] Tab headers in bottom TabControl: same style
- [ ] Active tab indicator: 2px accent-color bottom border
- [ ] Tab hover: subtle background change (200ms transition)

### M5: Empty States

- [ ] Explorer empty state: "No folder open. Use File → Open Folder (Ctrl+Shift+O)" — centered, with folder icon
- [ ] Git empty state: "No Git repository detected" — centered, with git icon
- [ ] Problems empty state: "No problems detected" — centered, with checkmark icon
- [ ] Output empty state: "No output yet" — centered

### M6: GridSplitter Polish

- [ ] Default: 4px wide, `panel.border` color
- [ ] Hover: 4px wide, `global.accent` color, 200ms transition
- [ ] Cursor: ColResize (horizontal) / RowResize (vertical)

### M7: Panel Visibility Persistence

- [ ] Save `IsSidebarVisible` and `IsBottomPanelVisible` to settings on change
- [ ] Restore on app launch via ISettingsService
- [ ] Default: both visible

---

## 3. Files to Modify

| File | Change |
|------|--------|
| `src/MainWindow.axaml` | Polish panel borders, headers, animations, splitter styles |
| `src/Views/FileExplorerView.axaml` | Refine header, empty state |
| `src/Views/GitPanelView.axaml` | Refine header, empty state |
| `src/Views/ProblemsView.axaml` | Refine header, empty state |
| `src/Views/OutputView.axaml` | Refine header, empty state |
| `src/Styles/ControlThemes.axaml` | Add TabControl tab strip styles, GridSplitter hover |

### Files NOT modified

- `src/Docking/` — removed from scope (Dock.Avalonia abandoned)
- `src/ViewModels/` — no ViewModel changes needed
- `src/Services/` — ISettingsService already handles persistence

---

## 4. Tests

- Unit: Panel visibility state round-trips through ISettingsService
- Manual: Toggle sidebar — smooth 200ms transition, no layout jump
- Manual: Toggle bottom panel — smooth 200ms transition, no layout jump
- Manual: Hover GridSplitter — color changes to accent
- Manual: All empty states render correctly
- Manual: All panels render correctly in Light and Dark themes
- Manual: Tab switching in sidebar and bottom panel — active indicator works

---

## 5. Exit Condition

- [ ] All 7 milestones (M1–M7) complete
- [ ] `dotnet build src/aero.csproj` passes (0 errors)
- [ ] `dotnet test tests` passes (all existing tests)
- [ ] Manual smoke test: all panels render, toggle animations smooth, empty states beautiful
- [ ] Both themes (Light/Dark) verified
