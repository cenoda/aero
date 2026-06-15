# Phase 9: Advanced Features

> Power-user features and LSP depth.

## Goal

Add multi-cursor, minimap, snippets, and advanced LSP features.

## Entry Condition

- Phase 8 complete (UI Polish)

## Exit Condition

- Multi-cursor editing works (Ctrl+D)
- Minimap renders code overview
- Snippets are configurable and expandable
- Go to definition, rename symbol, and format document work via LSP

## Checklist

- [ ] **Multi-cursor editing** — Ctrl+D to select next occurrence
- [ ] **Minimap** — scrollable code overview
- [ ] **Snippets** — configurable code snippets
- [ ] **Go to definition** — via LSP
- [ ] **Rename symbol** — via LSP
- [ ] **Format document** — via LSP

### Phase 9.5: Real Terminal (Optional)

> ⚠️ High difficulty. Only attempt if everything else is solid.

- [ ] **Pty.Net** — OS-specific PTY (Linux/Mac/Windows)
- [ ] **VtNetCore** — VT100/xterm escape code parsing
- [ ] **TerminalRenderer** — render to Avalonia canvas directly
- [ ] Interactive shell (bash / cmd / powershell)
- [ ] Multiple terminal tabs

## Related Documents

- `docs/LIBRARIES.md` — Pty.Net, VtNetCore (for 9.5)
- `docs/design/LSP_DESIGN.md` — LSP methods (definition, rename, formatting)
- `docs/architecture/IDE_CORE.md` — Advanced Features subsystem

## Notes

- Multi-cursor depends on AvaloniaEdit support. If unavailable, skip or document as limitation.
- Minimap can reuse the same text buffer with scaled-down rendering.
- LSP features in this phase are mostly wiring existing LSP methods to UI commands.
- Phase 9.5 is genuinely hard. Consider it a stretch goal, not a requirement.
