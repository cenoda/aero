# M3 Implementation Plan - Diagnostics (Phase 4)

## Information Gathered

### Existing Codebase Analysis

1. **Current LSP Infrastructure:**
   - `LSPSession.cs` - Has `PublishDiagnosticsReceived` event firing on background JSON-RPC thread
   - `LSPManager.cs` - Manages sessions, has blocking sync init (R8.1 issue)
   - `PublishDiagnosticsParams.cs` - Raw LSP DTO with JToken diagnostics array
   - `LSPDiagnosticsEventArgs.cs` - Event args wrapping the params

2. **R8.1 Issue - Session init async:**
   - In `LSPManager.OnFolderOpened()` (lines ~135-149):
   ```csharp
   var initialized = newSession.InitializeAsync("csharp-ls", ToFileUri(folderPath), CancellationToken.None)
       .GetAwaiter()
       .GetResult();
   ```
   This blocks the UI thread. Need to make async.

3. **R8.2 Issue - Sync kind:**
   - In `LSPSession.IsFullDocumentSyncSupported()` - currently strict check for `== 1`
   - Need to accept 1 (full) OR 2 (incremental) per R8.2 requirement

4. **MessageBus Integration:**
   - `Messages.cs` has `DocumentTextChanged` for sync, no `DiagnosticsUpdated` message
   - Need to add `DiagnosticsUpdated` message

5. **Thread Safety Pattern:**
   - From `ShellViewModel.StatusMessage`:
   ```csharp
   var dispatcher = GetUiDispatcher();
   if (dispatcher != null && !dispatcher.CheckAccess())
       dispatcher.Post(() => /* update */);
   ```

6. **DI Registration:**
   - `App.axaml.cs` registers LSPManager as singleton
   - DiagnosticStore needs to be added as singleton

## Plan

### Round 8 Fixes (R8.1 and R8.2):

1. **R8.1 - Make session init async in LSPManager:**
   - Modify `OnFolderOpened()` to run session creation + init on background thread
   - Assign `_session` under lock once initialized
   - Use `Task.Run` pattern to not block UI thread

2. **R8.2 - Relax sync kind validation:**
   - Modify `LSPSession.IsFullDocumentSyncSupported()` to accept both 1 (full) and 2 (incremental)
   - Keep Phase 4 sending full (not incremental) regardless

### M3 Deliverables:

1. **Diagnostic Models (in `src/Languages/Models/`):**
   - Create `Diagnostic.cs` - UI-friendly record: severity, file URI/path, range (start/end line+character), message, source/code

2. **DiagnosticStore.cs (in `src/Languages/`):**
   - Plain class (no interface per R7.1)
   - Holds latest diagnostics per file URI (replace, never accumulate)
   - Flattens to workspace-wide list
   - Raises/publishes `DiagnosticsUpdated` message when set changes

3. **Messages.cs:**
   - Add `DiagnosticsUpdated` record

4. **LSPManager:**
   - Handle `LSPSession.PublishDiagnosticsReceived` event
   - Convert payload to diagnostic models
   - Push into DiagnosticStore
   - Marshal to UI thread using guarded GetUiDispatcher()
   - Clear file diagnostics on didClose

5. **Editor Rendering:**
   - Implement `IBackgroundRenderer` for diagnostics
   - Register via `TextEditor.TextView.BackgroundRenderers`
   - Use `TextSegment` for ranges (reference AvaloniaEdit.Search pattern)
   - Wire in `EditorView.axaml.cs` (same place as TextMate wiring)
   - Update markers when diagnostics change

6. **DI Changes:**
   - Register DiagnosticStore as singleton
   - Inject into LSPManager

7. **Tests:**
   - `DiagnosticStoreTests.cs` - replace-not-accumulate, flattening/ordering, DiagnosticsUpdated raised, clear-on-close
   - Extend LSPManagerTests - drive publishDiagnostics through fake peer and verify it lands in store

## Files to Create or Modify

### New Files:
- `src/Languages/Models/Diagnostic.cs` - diagnostic record
- `src/Languages/DiagnosticStore.cs` - storage class
- `tests/Languages/DiagnosticStoreTests.cs` - tests

### Existing Files Modified:
- `src/Languages/LSPSession.cs` - R8.2 sync kind check
- `src/Languages/LSPManager.cs` - R8.1 async init + diagnostics handling
- `src/Languages/EditorDiagnosticRenderer.cs` - background renderer
- `src/Core/Messages.cs` - add DiagnosticsUpdated
- `src/Views/EditorView.axaml.cs` - register renderer
- `src/App.axaml.cs` - register DiagnosticStore singleton
- `tests/Languages/LSPManagerTests.cs` - extend with diagnostics tests

## Implementation Order

1. Fix R8.1 and R8.2 (gate for smoke tests)
2. Create diagnostic model
3. Create DiagnosticStore with DiagnosticsUpdated message
4. Add DiagnosticsUpdated to Messages.cs
5. Integrate into LSPManager with thread marshaling
6. Add IBackgroundRenderer rendering
7. Wire in EditorView.axaml.cs
8. Add DI registration
9. Add tests
10. Build and test

## Acceptance Criteria

- `dotnet build src/aero.csproj` clean (0 warnings)
- `dotnet test tests` green across 3 consecutive runs
- C# syntax error appears in DiagnosticStore and is rendered in editor
- Diagnostics replaced per URI (not accumulated)
- Diagnostics cleared on close
- R8.1 keeps UI responsive on folder open
