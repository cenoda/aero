# 8.2 — Theme Engine (115 Color Tokens + JSON Override)

**Goal:** Light and Dark themes with 115 semantic color tokens, fully overridable by the user via JSON.

> 📋 **Complete token inventory:** See [`TOKENS.md`](TOKENS.md) for the full list of all 115 tokens organized by area, with descriptions and naming rules.

**Scope:**
- Define **115 semantic color tokens** covering:
  - Global: accent, error, warning
  - Window: background, foreground, border, shadow
  - Editor: background, foreground, line numbers, selection, find highlight, bracket match, indent guides, gutter
  - Tabs: active/inactive background/foreground, top accent border
  - Panels: sidebar background, status bar, panel headers, section backgrounds, hover/active states, scrollbar
  - Status Bar, Menu, Button, Input, Scrollbar, Dialog, Find/Replace
  - Git: diff gutter/background/text (10 tokens), graph canvas (5), status indicators (5)
  - Debug: breakpoints, current line, stack frame, step overlay
  - Syntax: bridge tokens for TextMate palette
  - Notifications, Badges/Tags
- Create **Light** and **Dark** presets stored as `ResourceDictionary` files in `src/Styles/`
- **User JSON override** — `~/.aero/theme-override.json` lets the user customize every single color token (VS Code `workbench.colorCustomizations` style)
- Switch via `App.Current.RequestedThemeVariant`; status bar shows current theme
- `Ctrl+Shift+T` or `View → Toggle Theme`
- Color token naming follows the convention defined in **8.9 Design System** (e.g. `editor.background`, `panel.border`, `button.hoverBackground`)

**Dependencies:**
- **8.9 Design System** — must be completed first (defines color token naming convention and base ResourceDictionary structure)

**Exit condition:**
- 115 semantic color tokens defined and applied across all panels and editor
- Light and Dark presets switch consistently
- `~/.aero/theme-override.json` overrides any subset of tokens
- Theme switching is instant with no restart

**Tests:**
- Unit: Verify all token keys resolve to non-null values in both Light and Dark presets
- Unit: JSON override merges correctly with preset (partial override, empty override, full override)
- Integration: Theme switch round-trip (Light→Dark→Light) via command and settings
- Integration: `~/.aero/theme-override.json` is created on first edit, read on startup

