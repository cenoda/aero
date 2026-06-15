# Phase 8: UI Polish

> Make it feel like a real IDE.

## Goal

Add docking, themes, command palette, keybindings, welcome page, and settings.

## Entry Condition

- Phase 7 complete (Git Integration)

## Exit Condition

- Panels are dockable and draggable
- Light/dark theme switch works
- Command palette (Ctrl+Shift+P) opens and searches commands
- Keybindings are customizable
- Welcome page shows on startup
- Settings page allows editing preferences

## Checklist

- [ ] **Dockable panels** — drag to rearrange layout
- [ ] **Theme system** — light/dark switch
- [ ] **Command palette** — Ctrl+Shift+P fuzzy search
- [ ] **Keybinding config** — customizable shortcuts
- [ ] **Welcome page** — recent projects, new file, etc.
- [ ] **Settings page** — preferences UI (font, theme, tab size, etc.)

## Related Documents

- `docs/LIBRARIES.md` — Dock.Avalonia, DialogHost.Avalonia, FuzzySharp, Material.Icons.Avalonia
- `docs/design/PANELS_AND_DOCKING.md` — Docking approach (Option A vs B)
- `docs/architecture/IDE_CORE.md` — Settings subsystem

## Notes

- This is the **gateway to Agent Track**. Do not start Phase A1 until this phase is complete.
- Dock.Avalonia migration may require refactoring existing panel layout (Option B → Option A).
- Theme system should affect all panels, editor, and status bar consistently.
- Settings persistence: `~/.aero/settings.json` via Microsoft.Extensions.Configuration.Json.
