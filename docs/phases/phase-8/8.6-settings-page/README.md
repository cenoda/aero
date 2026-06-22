# 8.6 — Settings Page

**Goal:** A single dialog for user preferences, persisted to `~/.aero/settings.json`.

**Scope:**
- Sections: Font (family, size), Theme (light/dark, plus JSON override path), Tab Size, Editor (word wrap, line numbers, render whitespace), **Layout Mode selector** (Tile/Freeform)
- Read/write JSON via `Microsoft.Extensions.Configuration.Json`
- Settings take effect immediately; no restart required
- Use `ISettingsService` (defined in **8.7**) as the shared persistence layer

**Dependencies:**
- **8.7 Workspace Persistence** — provides the `ISettingsService` / `SettingsService` infrastructure that 8.6 reads from and writes to. The settings page is a UI on top of this service.
- **8.9 Design System** — dialog styling (spacing, typography, form controls)
- **8.2 Theme Engine** — color tokens preview in the theme selector
- **8.1 Dockable Panels** — Layout Mode selector (Tile/Freeform) depends on 8.1 modes being implemented

**Exit condition:** User can change preferences and they persist across restarts. All settings apply immediately.

**Tests:**
- Unit: Settings read/write round-trip (write settings → read back → verify equality)
- Unit: Settings file is created on first write with defaults
- Integration: Changing a setting (e.g. font size) is immediately reflected in the editor
- Integration: Setting persists after app restart
- Integration: Corrupted settings file falls back to defaults (graceful degradation)

