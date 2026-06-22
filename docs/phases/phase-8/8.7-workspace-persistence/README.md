# 8.7 — Workspace Persistence

**Goal:** Remember IDE state across restarts. Provide the shared `ISettingsService` infrastructure that 8.4, 8.6, and 8.1 depend on.

**Scope:**
- Define `ISettingsService` / `SettingsService` as the shared persistence layer used by 8.4 (recent folders), 8.6 (user preferences), and 8.1 (layout state)
- Remember last opened folder path
- Remember open file paths and active tab
- Remember window size, position, and maximized state
- Remember recent folders list (for 8.4 Welcome Page)
- Store workspace state in `~/.aero/workspace.json`
- Store user preferences in `~/.aero/settings.json` (via `Microsoft.Extensions.Configuration.Json`)
- **Note:** This sub-phase is primarily the infrastructure layer. The UI for settings (8.6) and the welcome page (8.4) build on top of it.

**Dependencies:**
- None — this is a foundational service that other sub-phases depend on
- **Should be implemented early** in Phase 8, right after 8.9 Design System

**Exit condition:** Closing and reopening the IDE restores folder, files, and window position. `ISettingsService` is registered in DI and consumed by at least one other sub-phase (8.4 or 8.6).

**Tests:**
- Unit: Write workspace state → read back → verify all fields match (folder, files, window position, maximized state)
- Unit: Write user settings → read back → verify all fields match
- Unit: Corrupted JSON falls back to defaults gracefully
- Integration: Close and reopen IDE → workspace state restored
- Integration: Recent folders list persists across restarts with correct ordering (most recent first)

