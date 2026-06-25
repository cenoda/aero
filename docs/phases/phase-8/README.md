# Phase 8: Core UI Polish

> Make it feel like a real IDE — with finite, bounded scope.

## Goal

Add dockable panels, theme system, command palette, welcome page, icons, settings, and persistence. Each sub-phase lives in its own folder with a focused plan.

## Scope Principle

**Finite and bounded. No infinite configurability.** Each sub-phase (8.1–8.9) has a clear delivered-at boundary. Keybinding *editing* is deferred to Phase 9; Phase 8 only provides a read-only reference.

**All sub-phases include test expectations.** At minimum: unit tests for logic, integration tests for round-trips, and manual verification for visual/UI behavior. See each sub-phase README for specific test requirements.

## Layout Philosophy

Aero uses a **fixed sidebar+editor+bottom-panel layout** with GridSplitter resizing. This is the layout 95% of IDE users actually use (VS Code's default). Dock.Avalonia was evaluated and abandoned after two failed integration attempts — the library's internal rendering is too opaque to debug effectively.

| Zone | Content | Width/Height |
|------|---------|-------------|
| Left sidebar | Explorer + Git tabs | 250px (resizable) |
| Center | Editor (tabs + AvaloniaEdit) | Flex (fills remaining) |
| Bottom panel | Problems + Output tabs | 150px (resizable) |
| Bottom | Status bar | Auto |

## Entry Condition

- Phase 7 complete (Git Integration)

## Exit Condition

- Panels have polished headers, borders, and spacing matching the design system
- Sidebar and bottom panel toggle with smooth 200ms animations
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
| 8.1 | [`8.1-panel-polish/`](8.1-panel-polish/) | Panel polish — headers, borders, animations, empty states, splitter hover. No Dock.Avalonia. |
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
5.  8.1  Panel Polish           ← Depends on 8.9, 8.5, 8.7; polish existing Grid layout
6.  8.3  Command Palette        ← Depends on 8.9, 8.2; shared CommandRegistry
7.  8.8  Keybinding Display     ← Shares CommandRegistry with 8.3; can be parallel with 8.3
8.  8.4  Welcome Page           ← Depends on 8.7 (recent folders), 8.9, 8.2
9.  8.6  Settings Page          ← Depends on 8.7 (ISettingsService), 8.2, 8.9
```

## Dependencies Summary

```
8.9 ──┬── 8.2 ──┬── 8.3 ── 8.8 (shared CommandRegistry)
      │         │
      │         └── 8.1 (panel polish, no Dock.Avalonia)
      │
      └── 8.7 ──┬── 8.4
                └── 8.6
      8.5 (independent, parallel)
```

## Notes

- This is the **gateway to Agent Track**. Do not start Phase A1 until this phase is complete.
- Dock.Avalonia was evaluated and abandoned (two failed attempts). The fixed Grid+GridSplitter layout is sufficient for v1.
- `DialogHost.Avalonia` requires Avalonia >= 12 (incompatible with 11.3). 8.3 Command Palette uses a custom overlay instead.
- Settings persistence: `~/.aero/settings.json` via Microsoft.Extensions.Configuration.Json.
- All sub-phases include test expectations; see each README for specifics.
