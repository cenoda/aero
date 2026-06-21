# TODO: Editor Diagnostic Rendering Fixes

## Issues Identified

1. **TryResolveDiagnosticStore() always returns null** – Reflects for `Services` property on `ShellViewModel`; no such property exists. Violates AGENTS (no service locator / no static service access).

2. **URI mismatch** – EditorView uses `new Uri(path).ToString()` while LSPManager/DiagnosticStore uses `.AbsoluteUri`. Different strings for paths with spaces (`%20` vs space).

3. **Renderer closure captures wrong document** – `_diagnosticRenderer` created once with capture of initial `doc`; after tab switch refers to wrong file.

4. **Renderer added to only one editor** – Added only at registration time, not when switching tabs/new editors.

5. **Nothing repaints on DiagnosticsUpdated** – No subscription to `DiagnosticsUpdated` message.

## Fix Plan

### Step 1: Pass DiagnosticStore via message (AGENTS-compliant)
- Add a `DiagnosticStoreReady` message published by App.axaml.cs when DiagnosticStore is created
- EditorView subscribes to this message to receive DiagnosticStore reference

### Step 2: Fix URI generation to match LSPManager
- Change `GetActiveDocumentUri()` to use `.AbsoluteUri` like LSPManager.ToFileUri()

### Step 3: Make renderer URI dynamic per draw call
- Change `EditorDiagnosticRenderer` to accept a `Func<TextDocument?>` that returns current active document at draw time
- Or store the active document reference and refresh on each Draw call

### Step 4: Add renderer to each active editor
- Move renderer registration to `ResubscribeEditor()` when editor becomes active
- Add to each new editor's BackgroundRenderers on switch

### Step 5: Subscribe to DiagnosticsUpdated
- EditorView subscribes to DiagnosticsUpdated message
- On receipt, call `TextView.Redraw()` on all active editors with the renderer

## Files to Edit
- `src/Core/Messages.cs` – Add `DiagnosticStoreReady` message
- `src/App.axaml.cs` – Publish `DiagnosticStoreReady` after creating DiagnosticStore
- `src/Views/EditorView.axaml.cs` – Fix TryResolveDiagnosticStore, URI, renderer registration, add DiagnosticsUpdated subscription
- `src/Languages/EditorDiagnosticRenderer.cs` – Accept Func<TextDocument?> instead of captured doc
