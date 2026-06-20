# M3 Implementation Complete - TODO

## Phase 4 M3 Task List

### Round 8 Fixes (Completed)
- [x] R8.1 - Make LSPManager session init async (non-blocking UI)
- [x] R8.2 - Relax sync kind check to accept incremental (2)

### M3 Deliverables (Completed)
- [x] Create Diagnostic.cs model in Models/
- [x] Create DiagnosticStore.cs in Languages/
- [x] Add DiagnosticsUpdated message to Messages.cs
- [x] Integrate diagnostics handling into LSPManager
- [x] Implement IBackgroundRenderer for editor rendering
- [x] Wire renderer in EditorView.axaml.cs
- [x] Register DiagnosticStore in DI
- [x] Add DiagnosticStoreTests.cs
- [x] Extend LSPManagerTests with diagnostics tests

### Build and Verify (COMPLETE)
- [x] dotnet build src/aero.csproj clean (0 warnings)
- [x] dotnet test tests green (3 consecutive runs): 276/276
- [ ] Manual smoke test (not automated)
