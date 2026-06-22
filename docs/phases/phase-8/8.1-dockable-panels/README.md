# 8.1 — Dockable Panels

**Goal:** Convert the existing hard-coded layout into a flexible system that supports Tile Mode, Freeform Mode, and tear-away windows.

**⚠️ This sub-phase is split into three ordered milestones to manage complexity:**

### 8.1a — Freeform Mode (Dock.Avalonia wiring)
**Scope:**
- Wire existing panels (sidebar-left with Explorer+Git tabs, editor-center, bottom-panel with Problems+Output tabs) into Dock.Avalonia
- Panels become draggable, resizable, hideable, and can be rearranged by the user
- No new panels created; existing panels are converted to Dock.Avalonia `DockableContent`
- Mode switching in settings; no restart required

**Dependencies:**
- **8.9 Design System** — spacing/padding values for panel headers, borders, and sizes
- **8.5 Icon Decision** — icon for panel headers (or text glyphs as fallback)

**Exit condition (8.1a):** Existing panels work in Freeform Mode — user can drag, resize, rearrange, and hide all panels. Mode switchable in settings.

### 8.1b — Tile Mode (auto-layout)
**Scope:**
- Auto-layout with tiling + stack (tab) support
- Panels tile when placed side-by-side, stack into tabs when overlapped (notebook style)
- Keyboard-navigation optimized
- **Even in Tile Mode, manual window adjustment must be possible** (avoids Hyperland's limitation)
- Mode switching instant with no restart

**Dependencies:**
- 8.1a must be complete (Tile Mode builds on top of dock infrastructure)

**Exit condition (8.1b):** Tile Mode works with auto-tiling, stack/merge behavior, and full keyboard navigation. User can override auto-layout manually.

### 8.1c — Tear-Away Windows (Chrome-style)
**Scope:**
- Panels can be dragged out of the main window into standalone OS windows
- Implemented via runtime `Window` creation + content transfer in Avalonia
- Re-dockable: tear-away windows can be re-merged into the main window
- Focus management: tear-away windows behave correctly with Alt+Tab, DPI changes

**Dependencies:**
- 8.1a must be complete (tear-away extends the dock infrastructure)

**Exit condition (8.1c):** Panels can be torn out into separate OS windows and re-docked. All panels work correctly in tear-away state.

**Tests (all milestones):**
- Unit: Panel visibility commands toggle panels correctly in both modes
- Integration: Panel layout round-trip (drag → save → reopen → restore position)
- Integration: Mode switch from Freeform → Tile → Freeform preserves layout
- Manual: Tear-away panel works across monitor DPI changes
- Manual: All keyboard shortcuts work in Tile Mode

