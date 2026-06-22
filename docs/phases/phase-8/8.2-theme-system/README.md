# 8.2 — Theme Engine (80–100 Color Tokens + JSON Override)

**Goal:** Light and Dark themes with 80–100 semantic color tokens, fully overridable by the user via JSON.

**Scope:**
- Define **80–100 semantic color tokens** covering:
  - Editor: background, foreground, line numbers, selection, find highlight, inline hints, bracket match
  - Panels: sidebar background, status bar, panel headers, tab active/inactive, button/hover, scrollbar
  - Git: diff inserted/removed/modified, branch indicator
  - Debug: breakpoint, current line, step overlay
  - Border/divider colors, shadow colors, overlay backgrounds
- Create **Light** and **Dark** presets stored as `ResourceDictionary` files in `src/Styles/`
- **User JSON override** — `~/.aero/theme-override.json` lets the user customize every single color token (VS Code `workbench.colorCustomizations` style)
- Switch via `App.Current.RequestedThemeVariant`; status bar shows current theme
- `Ctrl+Shift+T` or `View → Toggle Theme`
- Color token naming follows the convention defined in **8.9 Design System** (e.g. `editor.background`, `panel.border`, `button.hoverBackground`)

**Dependencies:**
- **8.9 Design System** — must be completed first (defines color token naming convention and base ResourceDictionary structure)

**Exit condition:**
- 80–100 semantic color tokens defined and applied across all panels and editor
- Light and Dark presets switch consistently
- `~/.aero/theme-override.json` overrides any subset of tokens
- Theme switching is instant with no restart

**Tests:**
- Unit: Verify all token keys resolve to non-null values in both Light and Dark presets
- Unit: JSON override merges correctly with preset (partial override, empty override, full override)
- Integration: Theme switch round-trip (Light→Dark→Light) via command and settings
- Integration: `~/.aero/theme-override.json` is created on first edit, read on startup

