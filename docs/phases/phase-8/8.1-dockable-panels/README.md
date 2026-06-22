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

---

## Architecture Decision: Option A — Constrained Dock.Avalonia (2026-06-22)

**Selected approach:** Constrain Dock.Avalonia's layout model rather than building a separate tiling engine.

**Why Option A:**
1. Reuses existing Dock.Avalonia infrastructure from 8.1a (faster to implement)
2. "Manual adjustment" requirement met via unlock mechanism — constraint is default, not absolute
3. Simpler maintenance — single code path for both modes
4. Option B (separate tiling engine) is 2-3x work, risks delaying Phase 8

**How it works:**
- Tile Mode uses Dock.Avalonia's `ProportionalStackPanel` with pre-defined dock node sizes
- Default layout (configurable in settings):
  - Sidebar: 250px fixed width
  - Editor: flex (fills remaining space)
  - Bottom panel: 150px fixed height
- User can drag splitters to adjust → layout updates proportionally
- "Reset to Tile" button restores default proportions
- Stack/tab behavior uses Dock.Avalonia's native tab grouping

**Implementation notes:**
- Create `TileLayoutFactory` that produces pre-configured `DockNode` trees
- Store tile proportions in settings (not hard-coded)
- Mode switch: swap the `DockController`'s layout root from `FreeformLayout` to `TileLayoutFactory`
- Manual override: allow proportional resizing; "Reset" button available in View menu

### 8.1c — Tear-Away Windows (Chrome-style)
**Scope:**
- Panels can be dragged out of the main window into standalone OS windows
- Implemented via runtime `Window` creation + content transfer in Avalonia
- Re-dockable: tear-away windows can be re-merged into the main window
- Focus management: tear-away windows behave correctly with Alt+Tab, DPI changes

**Technique Validation (2026-06-22):** ✅ Validated

The direct transfer technique (moving the same `UserControl` instance between windows) works in Avalonia 11.3:
- `DataContext` is stored on the Control → preserved on transfer
- `StyledProperty` values are stored on the Control → preserved on transfer
- `FindResource()` resolves from the new window's resource chain → works as expected

See [TearAwaySpikeTest.cs](../../tests/Languages/TearAwaySpikeTest.cs) for design analysis.

**Dependencies:**
- 8.1a must be complete (tear-away extends the dock infrastructure)

**Exit condition (8.1c):** Panels can be torn out into separate OS windows and re-docked. All panels work correctly in tear-away state.

**Tests (all milestones):**
- Unit: Panel visibility commands toggle panels correctly in both modes
- Integration: Panel layout round-trip (drag → save → reopen → restore position)
- Integration: Mode switch from Freeform → Tile → Freeform preserves layout
- Manual: Tear-away panel works across monitor DPI changes
- Manual: All keyboard shortcuts work in Tile Mode

---

## Implementation Notes

### Dock.Avalonia Layout Serialization API (confirmed 2026-06-22)

**Package:** `Dock.Serializer.SystemTextJson` (11.3.12.1)

**Core API:**
```csharp
using Dock.Serializer.SystemTextJson;

// Mark your layout model type
[DockJsonSerializable]
public class MyLayoutModel
{
    public List<PanelState> Panels { get; set; }
}

// Serialize to JSON string
var json = DockSerializer<MyLayoutModel>.Serialize(layout);

// Deserialize back
var restored = DockSerializer<MyLayoutModel>.Deserialize(json);

// Or use Stream-based APIs
DockSerializer<MyLayoutModel>.Save(stream, layout);
var restored = DockSerializer<MyLayoutModel>.Load(stream);
```

**Notes:**
- Uses System.Text.Json with source generation (fast, trim-friendly)
- Requires `[DockJsonSerializable]` attribute on all serializable types
- Layout model must match Dock.Avalonia's `IRootDock` structure
- For custom panel content, mark panel view models with `[DockJsonSerializable]` and ensure they implement `IDockable`

