# Phase 3: Syntax Highlighting

> Code should look like code.

## Goal

Add syntax highlighting for common languages via TextMate grammars.

## Entry Condition

- Phase 2 complete (file explorer, project system)

## Exit Condition

- Opening a file auto-detects language from extension
- Syntax highlighting applies immediately
- Status bar shows current language
- Supported: C#, JSON, XML, Markdown (minimum)

## Checklist

- [ ] **LanguageDefinition** registry (C#, JSON, XML, Markdown, etc.)
- [ ] **TextMate grammar loader** — load .tmLanguage JSON
- [ ] Wire grammar to AvaloniaEdit highlighting
- [ ] Auto-detect language from file extension
- [ ] Status bar shows current language

> **Status:** Active — Planning

## Related Documents

- `docs/LIBRARIES.md` — AvaloniaEdit.TextMate, TextMateSharp.Grammars
- `docs/design/EDITOR.md` — Syntax Highlighting section

## Notes

- TextMateSharp.Grammars bundles 100+ grammars. Just wire them.
- Language registry should be extensible for later plugin support.
- Fallback to plain text if no grammar matches.
