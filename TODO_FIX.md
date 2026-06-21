# Editor Diagnostic Rendering Fixes (Historical Documentation)

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

---

## PHASE 7 ISSUES (2026-06-22)

### Medium Priority

1. **GetFileDiffAsync always compares HEAD vs WorkingDirectory** — staged/unstaged diffs are identical
2. **Diff gutter text is invisible** — Foreground bound to same color as background
3. **Diff hunk metadata is all zeros** — line numbers start from 0
4. **GitServiceFactory.Detect() is not thread-safe** — no lock on cache access
5. **GitViewModel.Dispose() incorrectly owns factory disposal** — DI violation

### Low Priority

6. Missing integration tests for Stage/Unstage/Commit round-trips
7. CheckoutAsync uses fragile string matching on exception messages
8. StageAllAsync/UnstageAllAsync do N+1 redundant refreshes
9. `_lastRefresh` uses non-monotonic `DateTime.UtcNow`
10. All diff content lines rendered in bold
