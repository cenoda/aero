# Phase 8.1 — TOFIX

> Code quality issues found during review. All items must be resolved before Phase 8.1 is declared complete.

---

## Pre-Implementation Findings (2026-06-25)

---

### R1.1 `SettingsModel` missing panel-state properties *(priority: high, BLOCKER for M7)*

**Description:** `src/Models/Settings/SettingsModel.cs` does not contain
`IsSidebarVisible`, `IsBottomPanelVisible`, `SidebarWidth`, or `BottomPanelHeight`.
M7 cannot be implemented without them.

**Required fix:** Add the four properties to `SettingsModel` as described in M7.1 of the
implementation plan before implementing M7.

**Status:** [ ] Open

---

### R1.2 `ShellViewModel` `IsSidebarVisible` / `IsBottomPanelVisible` are not persisted *(priority: high)*

**Description:** `ToggleSidebar()` and `ToggleBottomPanel()` mutate `IsSidebarVisible` and
`IsBottomPanelVisible` but do not call `_settingsService.SaveSettingsAsync()`.
Panel state is therefore lost on restart.

**Required fix:** Extend `ToggleSidebar()` and `ToggleBottomPanel()` to save settings
after each toggle, as described in M7.3.

**Status:** [ ] Open

---

### R1.3 `Icons.axaml` missing chevron and status icons *(priority: medium)*

**Description:** M1 requires `Icon.ChevronLeft` and `Icon.ChevronDown` for collapse buttons.
M5 requires `Icon.CheckCircle` (Problems empty) and `Icon.GitBranch` (Git empty). None of
these are present in `src/Styles/Icons.axaml`.

**Required fix:** Add all four Phosphor icon `StreamGeometry` paths to `Icons.axaml`
before implementing M1 and M5.

**Status:** [ ] Open

---

### R1.4 Bottom panel `RowDefinition` uses fixed `MinHeight=100` / `MaxHeight=300` *(priority: low)*

**Description:** `MainWindow.axaml` bottom `TabControl` has `MinHeight="100"` and
`MaxHeight="300"` hardcoded. Once M3 animation is in place (binding `Height` to
`BottomPanelHeight`), the `MaxHeight` will conflict with the animation (it will clamp
the value). `MinHeight` will prevent full collapse to 0.

**Required fix:** When implementing M3, remove `MinHeight` and `MaxHeight` from the bottom
`TabControl`. Enforce a minimum drag size via the `RowDefinition MinHeight` on Row 2 instead.
The ViewModel should be the single source of truth for height.

**Status:** [ ] Open

---

### R1.5 `GridSplitter` `IsVisible` binding tied to `IsBottomPanelVisible` does not handle partial collapse *(priority: low)*

**Description:** The horizontal `GridSplitter` (Row 1) has `IsVisible="{Binding IsBottomPanelVisible}"`.
During the M3 animation the height transitions through intermediate values — if `IsVisible`
switches at the start of the animation the splitter will disappear before the panel finishes
collapsing.

**Required fix:** In M3, either (a) keep `IsVisible` switching at animation end using a
property change subscription, or (b) accept the current behaviour (splitter vanishes
immediately on toggle) as a known limitation. Document whichever choice is made.

**Status:** [ ] Open — resolve during M3

