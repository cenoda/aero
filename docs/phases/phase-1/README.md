# Phase 1: The Editor

> A usable text editor — the heart of the IDE.

## Goal

Implement a text editor with tabs, open/save, undo/redo, and find/replace.

## Entry Condition

- Phase 0 complete (DI, AvaloniaEdit package, directory skeleton)

## Exit Condition

- Can open, edit, save, and close multiple files via tabs
- Undo/Redo works across all open files
- Find/Replace overlay functions correctly
- Status bar shows cursor position (Ln X, Col Y)

## Checklist

- [x] **TextBuffer** — efficient gap-buffer or rope structure (using AvaloniaEdit's TextDocument)
- [x] **TextEditor view** — AvaloniaEdit integration with line numbers
- [x] **Document model** — open/close files, dirty flag
- [x] **Tabbed editor** — multiple open files with tabs
- [x] **File open/save** — Ctrl+O, Ctrl+S, file dialogs
- [x] **Undo/Redo** — built-in undo stack (Ctrl+Z / Ctrl+Y)
- [x] **Find/Replace** — Ctrl+F with overlay panel
- [x] Status bar shows cursor position (Ln X, Col Y)

## Related Documents

- `docs/design/EDITOR.md` — editor design notes (TextBuffer, Document lifecycle, Undo/Redo)
- `docs/architecture/IDE_CORE.md` — IDE component tree
- `docs/LIBRARIES.md` — AvaloniaEdit details

## Notes

- AvaloniaEdit provides the core text editing widget. Focus on wrapping it, not reimplementing it.
- Dirty flag must sync with tab header (dot indicator) and window title.
- Undo stack should be per-document, not global.
