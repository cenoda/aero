# Phase 8: Core UI Polish

> Make it feel like a real IDE — with finite, bounded scope.

## Goal

Add dockable panels, theme system, command palette, welcome page, icons, settings, and persistence. Each sub-phase lives in its own folder with a focused plan.

## Scope Principle

**Finite and bounded. No infinite configurability.** Each sub-phase (8.1–8.9) has a clear delivered-at boundary. Keybinding *editing* is deferred to Phase 9; Phase 8 only provides a read-only reference.

**All sub-phases include test expectations.** At minimum: unit tests for logic, integration tests for round-trips, and manual verification for visual/UI behavior. See each sub-phase README for specific test requirements.

## Layout Philosophy

Aero provides **two layout modes** that the user can switch between in settings:

| Mode | Description | Default |
|------|-------------|---------|
| **Tile Mode** | Auto-layout with tiling + stack (tab) support. Panels tile side-by-side or stack when overlapped (notebook style). Keyboard-navigation optimized. Suitable for multi-agent workflows. | ✅ Default |
| **Freeform Mode** | Traditional IDE docking. User drags panels to desired positions manually. Full Dock.Avalonia capabilities. | Switchable in settings |

Core principle: **Tile Mode must still allow manual window adjustment** (unlike Hyperland's limitation). Mode switching must be instant — no restart required.

## Entry Condition

- Phase 7 complete (Git Integration)

## Exit Condition

- Existing panels are wired into Dock.Avalonia (draggable, resizable, hideable)
- Light/Dark theme switch works via 115 semantic color tokens with JSON override
- Command Palette (Ctrl+Shift+P) opens and searches registered commands
- Welcome page shows when no files are open
- Icon library decision resolved and applied to file tree + tabs
- Settings page lets user edit font, theme, tab size, editor options
- Workspace state (last folder, open files, window position) persists across restarts
- Read-only keyboard shortcuts reference is available

## Sub-Phases

| # | Folder | Scope |
|---|--------|-------|
| 8.1 | [`8.1-dockable-panels/`](8.1-dockable-panels/) | Tile Mode / Freeform Mode / Tear-away windows via Dock.Avalonia. Mode switchable in settings. |
| 8.2 | [`8.2-theme-system/`](8.2-theme-system/) | Light/Dark with 115 semantic color tokens + JSON override — no custom theme editor |
| 8.3 | [`8.3-command-palette/`](8.3-command-palette/) | Ctrl+Shift+P, FuzzySharp on registered commands only |
| 8.4 | [`8.4-welcome-page/`](8.4-welcome-page/) | Recent projects + quick actions as a landing tab |
| 8.5 | [`8.5-icon-decision/`](8.5-icon-decision/) | Resolve TOFIX R3.1 — pick icon library or commit to custom |
| 8.6 | [`8.6-settings-page/`](8.6-settings-page/) | Single dialog, JSON persistence, immediate apply |
| 8.7 | [`8.7-workspace-persistence/`](8.7-workspace-persistence/) | Remember folder, open files, window state |
| 8.8 | [`8.8-keybinding-display/`](8.8-keybinding-display/) | Read-only shortcut reference page — editing deferred to Phase 9 |
| 8.9 | [`8.9-design-system/`](8.9-design-system/) | Spacing, radius, shadow, transition, typography foundation — exact values set by design agent |

## Related Documents

- `docs/LIBRARIES.md` — Dock.Avalonia, FuzzySharp, Phosphor embedded PathIcon assets (icon decision)
- `docs/roadmap/PHASES.md` — parent roadmap with checklist

## Recommended Execution Order

Phase 8 sub-phases have dependencies — the order below minimizes rework:

```
1.  8.9  Design System         ← Foundation: spacing, radius, shadows, typography, color naming
2.  8.7  Workspace Persistence ← Shared ISettingsService for 8.4, 8.6
3.  8.5  Icon Decision          ← Parallel with 8.7 (Phosphor embedded PathIcon integration for Phase 8)
4.  8.2  Theme Engine           ← Depends on 8.9 naming convention; 115 tokens + JSON override
5.  8.1a Dockable Panels (Freeform) ← Depends on 8.9, 8.5; wire existing panels into Dock.Avalonia
6.  8.3  Command Palette        ← Depends on DialogHost, 8.9, 8.2; shared CommandRegistry
7.  8.8  Keybinding Display     ← Shares CommandRegistry with 8.3; can be parallel with 8.3
8.  8.1b Dockable Panels (Tile) ← Depends on 8.1a
9.  8.4  Welcome Page           ← Depends on 8.7 (recent folders), 8.9, 8.2
10. 8.6  Settings Page          ← Depends on 8.7 (ISettingsService), 8.2, 8.9, 8.1
11. 8.1c Dockable Panels (Tear-away) ← Depends on 8.1a; lowest priority
```

## Dependencies Summary

```
8.9 ──┬── 8.2 ──┬── 8.3 ── 8.8 (shared CommandRegistry)
      │         │
      │         └── 8.1a ── 8.1b ── 8.1c
      │
      └── 8.7 ──┬── 8.4
                └── 8.6
      8.5 (independent, parallel)
```

## Notes

- This is the **gateway to Agent Track**. Do not start Phase A1 until this phase is complete.
- Dock.Avalonia is already in `src/aero.csproj` but not wired up.
- `DialogHost.Avalonia` requires Avalonia >= 12 (incompatible with 11.3). 8.3 Command Palette uses a custom overlay instead.
- Settings persistence: `~/.aero/settings.json` via Microsoft.Extensions.Configuration.Json.
- All sub-phases include test expectations; see each README for specifics.
