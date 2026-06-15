# Phase 4: Basic LSP Integration

> The editor becomes smart.

## Goal

Connect to language servers for diagnostics, completion, and the problems panel.

## Entry Condition

- Phase 3 complete (syntax highlighting, language registry)

## Exit Condition

- LSP session starts and initializes with a language server
- Diagnostics (red squigglies) appear on errors
- Problems panel lists all workspace diagnostics
- Ctrl+Space triggers code completion

## Checklist

- [ ] **LSPSession** — JSON-RPC over stdin/stdout to a language server
- [ ] **LSPManager** — spawn `csharp-ls` / `omnisharp` per project
- [ ] **Diagnostics** — red squigglies on errors
- [ ] **Problems panel** — list all diagnostics in workspace
- [ ] **Code completion** — Ctrl+Space triggers LSP completions

## Related Documents

- `docs/design/LSP_DESIGN.md` — LSPSession, LSPManager, JSON-RPC protocol
- `docs/LIBRARIES.md` — StreamJsonRpc, OmniSharp.Extensions.LanguageServer
- `docs/architecture/IDE_CORE.md` — Language Services subsystem

## Notes

- LSP is the biggest technical risk in the IDE track. If this phase stalls, create an issue immediately.
- Buffer sync (didOpen/didChange/didClose) must be correct or the server will give wrong results.
- Debounce diagnostics (300ms) and completion (100ms) to avoid flooding the server.
- Server binaries (csharp-ls, omnisharp) are external dependencies. Document installation in README.
