# TODO: Editor Diagnostic Rendering Fixes

## RESOLVED (2026-06-21)

All issues identified below have been fixed in the implementation:

1. ✅ **DiagnosticStore injection** – Now injected via `EditorViewModel` constructor, not service locator
2. ✅ **URI mismatch** – `GetActiveDocumentUri()` uses `.AbsoluteUri` to match LSPManager format
3. ✅ **Renderer closure** – Now uses `Func<TextDocument?>` to get current doc at draw time
4. ✅ **Renderer registration** – Registered in `ResubscribeEditor()` when editor becomes active
5. ✅ **DiagnosticsUpdated subscription** – `EditorViewModel` subscribes and raises `DiagnosticsChanged` event

## Current State (Phase 4 Complete)
- `EditorDiagnosticRenderer` receives `Func<TextDocument?>` at construction
- `EditorViewModel` passes `() => ActiveDocument` to renderer
- `EditorView` subscribes to `DiagnosticsChanged` event and redraws on updates
- All tests pass: 281/281
