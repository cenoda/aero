# 8.1 — Panel Polish & Layout Refinement

**Goal:** Polish the existing sidebar+editor+bottom-panel layout into a visually refined, smoothly animated IDE shell — without Dock.Avalonia.

**⚠️ Direction change (2026-06-25):** Dock.Avalonia integration failed twice (v1 and v2). The library's internal rendering is opaque and debugging is impractical. The existing Grid+GridSplitter layout already provides sidebar, editor, and bottom panel — the 95% use case. This sub-phase now focuses on making that layout beautiful rather than adding drag-to-rearrange.

## Why No Docking

| Dock.Avalonia promised | Reality |
|------------------------|---------|
| Drag panels anywhere | Only 1% of users do this |
| Tear-away windows | Nice-to-have, not must-have |
| Freeform layout | Two failed attempts, 13+ debug cycles each |

**What we have instead:** Sidebar (Explorer + Git tabs) | Editor | Bottom panel (Problems + Output tabs). This is VS Code's default layout. It works. It's what people actually use.

## Scope

### In scope (8.1)

- **Panel header polish** — consistent header bars with icon + title + close button, matching design system tokens
- **Panel border refinement** — clean 1px borders using `panel.border` token, consistent corner radius
- **Collapse/expand animations** — smooth 200ms transitions when toggling sidebar or bottom panel
- **Panel state persistence** — remember which panels are visible across restarts (via ISettingsService from 8.7)
- **Tab strip styling** — polished tab headers in sidebar and bottom panel TabControls
- **Empty states** — beautiful empty-state messages for panels with no content
- **Resize handle polish** — GridSplitter with hover highlight, matching design system

### Out of scope

- ~~Dock.Avalonia integration~~ — abandoned after two failed attempts
- ~~Tile Mode / Freeform Mode~~ — not needed for v1
- ~~Tear-away windows~~ — deferred indefinitely
- ~~Drag-to-rearrange panels~~ — fixed layout is sufficient

## Dependencies

- **8.9 Design System** ✅ — spacing, radius, shadows, transitions, typography tokens
- **8.5 Icon Decision** ✅ — Phosphor icons for panel headers
- **8.7 Workspace Persistence** ✅ — ISettingsService for panel visibility state

## Exit Condition

- All panels have polished headers with icon + title
- Panel borders and spacing match design system tokens
- Sidebar and bottom panel toggle with smooth 200ms transitions
- Panel visibility persists across restarts
- Tab strips in sidebar and bottom panel are visually refined
- Empty states are beautiful, not placeholder text
- GridSplitters have hover feedback

## Tests

- Unit: Panel visibility state round-trips through ISettingsService
- Manual: Toggle sidebar — transition is smooth, no layout jump
- Manual: Toggle bottom panel — transition is smooth, no layout jump
- Manual: Resize panels via GridSplitter — hover highlight works
- Manual: All panels render correctly in both themes (Light/Dark)

