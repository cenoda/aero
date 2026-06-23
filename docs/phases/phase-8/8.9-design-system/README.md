# 8.9 — Design System (Foundation)

> ⚠️ **Execute this sub-phase FIRST.** All other Phase 8 sub-phases (8.1–8.8) reference this design system for spacing, corner radius, shadows, transitions, typography, borders, and color token naming. Establishing this foundation early prevents rework.

> Area for the design agent to fill. Here we only define the scope. Specific values are specified below.

## Goal

Foundation that defines visual consistency for Aero's entire UI.  
All Phase 8 kid phases (8.1~8.8) are implemented based on this design system.

## Scope

### 1. Spacing Scale (padding/margin/gap)

Hierarchy of spacing used across the entire UI.
- Base unit: `4px` (4px grid)
- Scale: `4px`, `8px`, `12px`, `16px`, `24px`, `32px`
- Applied to: button inner padding, list item height, section gaps, panel margins

### 2. Corner Radius

Consistent rounding levels (Apple / Rider feel).
- Button: `6px`
- Input field: `6px`
- Panel: `8px`
- Popup/Dialog: `10px`
- Tab: `4px` (top only, bottom is 0)
- Scrollbar thumb: `4px`

### 3. Shadow System

Shadow hierarchy expressing depth (Avalonia BoxShadow).
- Subtle (surface-level): `0 1px 2px rgba(0,0,0,0.08)`
- Medium (elevated): `0 4px 12px rgba(0,0,0,0.12)`
- Popup (modal/dialog): `0 8px 24px rgba(0,0,0,0.16)`
- Applied on: hover, focus, modal states

### 4. Transition Timing

Animation defaults for smooth transitions (Avalonia Transitions).
- Duration: `200ms`
- Easing: `CubicOut` (ease-out)
- Applied to: hover, focus, panel open/close, tab switching, color changes

### 5. Typography

- Font family: `Inter` (already imported)
- Size scale: `11px` (status bar), `12px` (UI labels), `13px` (body), `14px` (tab titles), `16px` (heading)
- Applied to: body text, code, UI labels, tab titles, status bar

### 6. Border System

- Border width: `1px` (default)
- Border style: `Solid`
- Applied to: panel dividers, input fields, table grids

### 7. Color Token Naming Convention

- Token structure: `{area}.{property}` e.g. `editor.background`, `panel.border`, `button.hoverBackground`
- Defines key naming conventions used by Phase 8.2 Theme Engine

## Output

This design system is materialized into the following files under `src/Styles/`:
- `src/Styles/Spacing.axaml`
- `src/Styles/CornerRadius.axaml`
- `src/Styles/Shadows.axaml`
- `src/Styles/Transitions.axaml`
- `src/Styles/Typography.axaml`
- `src/Styles/Borders.axaml`

Each file is defined as an Avalonia ResourceDictionary and operates on top of Theme (8.2).

## Notes

- Specific values are filled in by the design agent
- Based on Apple / Rider feel
- Opposite direction from VS Code style (flat, angular, high-contrast)
