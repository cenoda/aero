# 8.2 — Color Token Inventory

> **Naming convention** (from 8.9 Design System): `{area}.{property}`  
> Example: `editor.background`, `panel.border`, `button.hoverBackground`

> All tokens listed below must be defined in **both** Light and Dark presets.  
> Actual hex values are assigned during implementation; this file defines names and semantics only.

---

## Token Count Summary

| Area | Count |
|------|-------|
| Global | 6 |
| Window | 5 |
| Editor | 14 |
| Tab | 7 |
| Panel (Sidebar / Bottom) | 12 |
| Status Bar | 4 |
| Menu | 5 |
| Button | 5 |
| Input | 5 |
| Scrollbar | 4 |
| Dialog / Overlay | 6 |
| Find / Replace | 3 |
| Git Diff | 10 |
| Git Graph | 5 |
| Git Status | 5 |
| Debug | 5 |
| Syntax Highlighting | 8 |
| Notification | 3 |
| Badge / Tag | 3 |
| **Total** | **103** |

---

## Global

| # | Token | Description |
|---|-------|-------------|
| 1 | `global.background` | Default app background (fallback) |
| 2 | `global.foreground` | Default app foreground (fallback) |
| 3 | `global.accent` | Primary accent color (links, focused controls, active indicators) |
| 4 | `global.accentHover` | Accent color on hover |
| 5 | `global.error` | Error / destructive action color |
| 6 | `global.warning` | Warning color |

---

## Window

| # | Token | Description |
|---|-------|-------------|
| 7 | `window.background` | Main window background |
| 8 | `window.foreground` | Main window default text |
| 9 | `window.border` | Outer window border / frame |
| 10 | `window.dropShadow` | Window drop shadow color |
| 11 | `window.inactiveBackground` | Window background when unfocused (platform-dependent) |

---

## Editor

| # | Token | Description |
|---|-------|-------------|
| 12 | `editor.background` | Editor canvas background |
| 13 | `editor.foreground` | Default text color |
| 14 | `editor.lineHighlightBackground` | Current line highlight background |
| 15 | `editor.selectionBackground` | Text selection background |
| 16 | `editor.findMatchBackground` | Current find match highlight |
| 17 | `editor.findMatchHighlightBackground` | Other find match highlights |
| 18 | `editor.wordHighlightBackground` | Symbol reference highlight (on hover) |
| 19 | `editor.bracketMatchBackground` | Matching bracket background |
| 20 | `editor.bracketMatchBorder` | Matching bracket border |
| 21 | `editorIndentGuide.background` | Indent guide line color |
| 22 | `editorIndentGuide.activeBackground` | Active indent guide color |
| 23 | `editorLineNumber.foreground` | Line number gutter text |
| 24 | `editorLineNumber.activeForeground` | Active line number |
| 25 | `editorGutter.background` | Gutter background (left of line numbers) |

---

## Tab

| # | Token | Description |
|---|-------|-------------|
| 26 | `tab.background` | Tab strip background |
| 27 | `tab.activeBackground` | Active tab background |
| 28 | `tab.activeForeground` | Active tab text |
| 29 | `tab.activeBorderTop` | Active tab top accent border (underline) |
| 30 | `tab.inactiveBackground` | Inactive tab background |
| 31 | `tab.inactiveForeground` | Inactive tab text |
| 32 | `tab.border` | Tab strip bottom/side border |

---

## Panel (Sidebar / Bottom)

| # | Token | Description |
|---|-------|-------------|
| 33 | `panel.background` | Sidebar and bottom panel background |
| 34 | `panel.foreground` | Panel default text |
| 35 | `panel.border` | Panel divider / border lines |
| 36 | `panel.headerBackground` | Panel header strip background |
| 37 | `panel.headerForeground` | Panel header text |
| 38 | `panel.sectionBackground` | Alternating section background within panels |
| 39 | `panel.sectionForeground` | Section text |
| 40 | `panel.activeItemBackground` | Highlighted / selected item in panel list |
| 41 | `panel.activeItemForeground` | Highlighted / selected item text |
| 42 | `panel.hoverBackground` | Hovered item background in panel |
| 43 | `panel.scrollbarBackground` | Panel scrollbar track |
| 44 | `panel.scrollbarThumb` | Panel scrollbar thumb |

---

## Status Bar

| # | Token | Description |
|---|-------|-------------|
| 45 | `statusBar.background` | Status bar background |
| 46 | `statusBar.foreground` | Status bar text |
| 47 | `statusBar.border` | Status bar top border |
| 48 | `statusBar.hoverBackground` | Status bar item hover |

---

## Menu

| # | Token | Description |
|---|-------|-------------|
| 49 | `menu.background` | Menu dropdown background |
| 50 | `menu.foreground` | Menu item text |
| 51 | `menu.selectionBackground` | Menu item hover/selection highlight |
| 52 | `menu.selectionForeground` | Menu item hover text |
| 53 | `menu.border` | Menu dropdown border |

---

## Button

| # | Token | Description |
|---|-------|-------------|
| 54 | `button.background` | Button background |
| 55 | `button.foreground` | Button text |
| 56 | `button.hoverBackground` | Button hover background |
| 57 | `button.activeBackground` | Button pressed/active background |
| 58 | `button.border` | Button border |

---

## Input

| # | Token | Description |
|---|-------|-------------|
| 59 | `input.background` | Text input / combo box background |
| 60 | `input.foreground` | Input text |
| 61 | `input.border` | Input border |
| 62 | `input.placeholderForeground` | Placeholder text |
| 63 | `input.focusBorder` | Input focus ring |

---

## Scrollbar

| # | Token | Description |
|---|-------|-------------|
| 64 | `scrollbar.background` | Scrollbar track |
| 65 | `scrollbarThumb.background` | Scrollbar thumb default |
| 66 | `scrollbarThumb.hoverBackground` | Scrollbar thumb on hover |
| 67 | `scrollbarThumb.activeBackground` | Scrollbar thumb while dragging |

---

## Dialog / Overlay

| # | Token | Description |
|---|-------|-------------|
| 68 | `dialog.background` | Modal / dialog background |
| 69 | `dialog.foreground` | Dialog text |
| 70 | `dialog.border` | Dialog border / shadow base |
| 71 | `dialog.overlayBackground` | Semi-transparent backdrop behind dialog |
| 72 | `dialog.buttonBackground` | Dialog action button background |
| 73 | `dialog.buttonForeground` | Dialog action button text |

---

## Find / Replace

| # | Token | Description |
|---|-------|-------------|
| 74 | `findReplace.background` | Find/Replace overlay background |
| 75 | `findReplace.border` | Find/Replace overlay border |
| 76 | `findReplace.inputBackground` | Find/Replace input field background |

---

## Git Diff

| # | Token | Description |
|---|-------|-------------|
| 77 | `diff.insertedGutter` | Gutter color for added lines |
| 78 | `diff.removedGutter` | Gutter color for removed lines |
| 79 | `diff.headerGutter` | Gutter color for diff hunk headers |
| 80 | `diff.contextGutter` | Gutter color for context lines |
| 81 | `diff.insertedBackground` | Background for added lines |
| 82 | `diff.removedBackground` | Background for removed lines |
| 83 | `diff.headerBackground` | Background for hunk header lines |
| 84 | `diff.insertedText` | Text color for added content |
| 85 | `diff.removedText` | Text color for removed content |
| 86 | `diff.conflict` | Merge conflict highlight |

---

## Git Graph

| # | Token | Description |
|---|-------|-------------|
| 87 | `graph.background` | Git graph canvas background |
| 88 | `graph.nodeBorder` | Commit node border |
| 89 | `graph.selectedNodeHalo` | Highlight around selected commit |
| 90 | `graph.connectionLine` | Solid connection line between commits |
| 91 | `graph.connectionLineMuted` | Dashed / muted connection lines |

---

## Git Status

| # | Token | Description |
|---|-------|-------------|
| 92 | `git.branchForeground` | Branch name text color |
| 93 | `git.modifiedForeground` | Modified file indicator |
| 94 | `git.stagedForeground` | Staged file indicator |
| 95 | `git.untrackedForeground` | Untracked file indicator |
| 96 | `git.conflictForeground` | Conflicting file indicator |

---

## Debug

| # | Token | Description |
|---|-------|-------------|
| 97 | `debug.breakpointForeground` | Breakpoint glyph |
| 98 | `debug.breakpointBackground` | Breakpoint line background |
| 99 | `debug.currentLineBackground` | Current execution line highlight |
| 100 | `debug.stackFrameBackground` | Current stack frame highlight |
| 101 | `debug.stepOverlay` | Step-into / step-over overlay glyph |

---

## Syntax Highlighting (Editor Theme Bridge)

> These tokens bridge the theme engine to the TextMate / LSP syntax colorer.
> They are **editor-internal** and not surfaced in JSON override (they come from the `.tmTheme` or TextMate theme file). Listed here so the theme engine can provide a coherent companion palette.

| # | Token | Description |
|---|-------|-------------|
| 102 | `syntax.keyword` | Keywords (`if`, `class`, `return`, …) |
| 103 | `syntax.string` | String literals |
| 104 | `syntax.number` | Numeric literals |
| 105 | `syntax.comment` | Comments |
| 106 | `syntax.function` | Function / method names |
| 107 | `syntax.type` | Type / class names |
| 108 | `syntax.variable` | Variable names |
| 109 | `syntax.operator` | Operators and punctuation |

> **Note:** Tokens 102–109 are informational only for the theme author. The actual syntax highlighting is driven by TextMate grammars (Phase 3). The theme engine should expose these as part of the Light/Dark preset JSON so advanced users who override them can do so — but they are **not** applied via Avalonia `DynamicResource`.

---

## Notification

| # | Token | Description |
|---|-------|-------------|
| 110 | `notification.background` | Toast / notification banner background |
| 111 | `notification.foreground` | Notification text |
| 112 | `notification.border` | Notification border |

---

## Badge / Tag

| # | Token | Description |
|---|-------|-------------|
| 113 | `badge.background` | Small badge / counter background |
| 114 | `badge.foreground` | Badge text |
| 115 | `badge.border` | Badge border |

---

## Grand Total: 115 tokens

> Adjusted upward from the original 80–100 estimate to cover every UI area identified
> in the current codebase audit. This ensures 8.2, 8.3, 8.4, and 8.6 all reference the
> same complete token set with no ad-hoc inventing.

---

## Token Naming Rules (from 8.9)

1. **Structure:** `{area}.{property}` — lowercase, dot-separated.
2. **Areas** map to UI regions: `editor`, `panel`, `tab`, `button`, `input`, `statusBar`, `menu`, `dialog`, `scrollbar`, `diff`, `graph`, `git`, `debug`, `syntax`, `notification`, `badge`, `findReplace`, `global`, `window`.
3. **Properties** are semantic: `background`, `foreground`, `border`, `hoverBackground`, `activeBackground`, `selectionBackground`, etc.
4. **Sub-properties** use camelCase: `lineHighlightBackground`, `activeItemForeground`.
5. **No hard-coded values** — every color goes through a token. Direct `#hex` in XAML is forbidden after Phase 8.

---

## JSON Override Example

Users override tokens via `~/.aero/theme-override.json`:

```json
{
  "editor.background": "#1a1a2e",
  "editor.foreground": "#e0e0e0",
  "global.accent": "#e94560",
  "panel.background": "#16213e",
  "statusBar.background": "#0f3460"
}
```

Any token not present in the override falls back to the active preset (Light or Dark).
