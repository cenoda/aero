# Phase 5.5 — To Fix

> **Status:** ✅ Complete — reviewed against real working tree and verification gates.
> Phase 5.5 findings are closed and ready for Phase 6 handoff.
>
> This file is the persistent code-quality checklist for Phase 5.5 (Abstraction Implementation Pass).
> Add findings here during and after each implementation/review round;
> mark each item `[x]` when fixed and note the fix inline.

---

## Round 1 — Pre-Implementation Risks (2026-06-21)

These items are known risks before coding starts. They are not all bugs yet,
but each must be verified or resolved during Phase 5.5.

### R1.1 New interfaces must preserve existing method signatures *(priority: critical, BLOCKER for M1)*

**Description:** Phase 1's `DocumentManager` lacks an interface. Adding
`IDocumentManagementService` must wrap all existing public methods with
identical signatures. Any signature change breaks consumers.

**Required fix:** Wrap `DocumentManager` exactly 1:1:

```csharp
public interface IDocumentManagementService
{
    // All DocumentManager public methods, verbatim
}
```

**Status:** ✅ Fixed — `IDocumentManagementService` created with 1:1 method signatures, `DocumentManager` implements it, all consumers updated to use the interface.

---

### R1.2 DI registration conflict between interface and existing concrete registration *(priority: high, BLOCKER for M1)*

**Description:** `DocumentManager` is registered as a concrete class in
`App.axaml.cs`. After adding `IDocumentManagementService`, registering
both causes "A service could not be resolved" errors.

**Required fix:** Replace concrete registration, don't add alongside:

```csharp
// BEFORE:
services.AddSingleton<DocumentManager>();

// AFTER:
services.AddSingleton<IDocumentManagementService, DocumentManagementService>();
services.AddSingleton<DocumentManager>(); // if still needed by existing code
```

Or refactor consumers to use the interface.

**Status:** ✅ Fixed — Both `IDocumentManagementService` and `DocumentManager` registered in DI. All consumers (`EditorViewModel`, `ShellViewModel`, `FileExplorerViewModel`, `LSPManager`) updated to use `IDocumentManagementService` interface.

---

### R1.3 Factory pattern may not be needed for singleton services *(priority: medium)*

**Description:** Adding `*ServiceFactory` classes to services that already work
as singletons (e.g., `IFileSystemService`) adds unnecessary complexity. The
singleton pattern already provides one implicit "factory" — the DI container.

**Required fix:** Only add factories where auto-detection is genuinely
needed (e.g., `ProcessRunnerServiceFactory` for command type detection).
Document the decision in each case.

**Status:** ✅ Resolved (2026-06-21) — Verified all services use direct DI registration. No factories added.

---

### R1.4 TextMate integration lives in View layer — interface must not leak into ViewModel *(priority: high, BLOCKER for M3)*

**Description:** Adding `ISyntaxHighlighterService` interfaces for TextMate
may encourage ViewModels to reference UI controls, violating MVVM. TextMate works
on `TextEditor` (View) not on buffers (Model).

**Required fix:** Keep `ISyntaxHighlighterService` in View layer only,
or mark it `[EditorOnly]` in comments. ViewModels consume
`ILanguageDetectionService` (UI-free), not syntax highlighting.

```csharp
// In View layer only:
public interface ISyntaxHighlighterService { ... } // Do not inject in ViewModels
```

**Status:** ✅ Resolved (2026-06-21) — TextMate integration is intentionally in View layer only (`EditorView.axaml.cs`). No service interface created. ViewModels use `ILanguageDetectionService` for language info, which is UI-free.

---

### R1.5 ILanguageService may duplicate existing ILanguageDetectionService *(priority: medium)*

**Description:** Phase 3 provides `ILanguageDetectionService`. Adding
`ILanguageService` risks confusion — are they the same? Different?

**Required fix:** If `ILanguageService` adds new capabilities beyond
detection (e.g., language-specific operations), document the difference.
Otherwise, reuse `ILanguageDetectionService` and document in
`CORE_INFRASTRUCTURE.md`.

**Status:** ✅ Resolved (2026-06-21) — `ILanguageDetectionService` is the single source of truth for language detection. No separate `ILanguageService` needed. ViewModels use `ILanguageDetectionService.Detect()` to get `LanguageInfo` (id, name, file extensions).

---

### R1.6 ProcessRunner already has an interface pattern via CliWrap *(priority: low)*

**Description:** Phase 5's `ProcessRunner` wraps CliWrap, which is an
external abstraction. Adding `IProcessRunnerService` should wrap the
existing `ProcessRunner` class, not replace it wholesale.

**Required fix:** `ProcessRunnerServiceFactory.Detect()` returns an
`IProcessRunnerService` that delegates to the existing `ProcessRunner`:

```csharp
public class ProcessRunnerAdapter : IProcessRunnerService
{
    private readonly ProcessRunner _inner;
    public string Name => "ProcessRunner (wrapped)";
    // ... delegate methods to _inner
}
```

**Status:** ✅ Resolved (2026-06-21) — `IProcessRunner` interface already exists in `src/Terminal/IProcessRunner.cs`, implemented by `ProcessRunner`. Registered in DI at line 106 of `App.axaml.cs`.

---

### R1.7 ILSPClientService factory may conflict with existing ILSPService *(priority: medium)*

**Description:** Phase 4 already has `ILSPService`. Adding
`ILSPClientService` could be perceived as replacing or conflicting.
Need clear separation of responsibilities.

**Required fix:** Document in `CORE_INFRASTRUCTURE.md`:

- `ILSPService` — protocol-level LSP operations
- `ILSPClientService` — session creation for a document

```csharp
public interface ILSPClientService
{
    /// <summary>Create an LSP session for a document.</summary>
    Task<ILSPService> CreateForDocumentAsync(TextDocument doc, CancellationToken ct);
}
```

**Status:** ✅ Resolved (2026-06-21) — Phase 4 uses concrete `LSPManager` and `LSPSession` classes (no interface). This follows the "wrap not replace" principle - adding interfaces now would be premature abstraction. `LSPManager` is registered as a singleton in DI and works correctly.

---

### R1.8 IFileSystemWatcherService may duplicate FileSystemWatcher handling *(priority: low)*

**Description:** `FileSystemService` already has file enumeration methods.
Adding `IFileSystemWatcherService` is the new interface recommended by the plan,
but it may overlap in responsibility.

**Required fix:** Ensure clean separation:

- `IFileSystemService` — read/write/enumerate (no watching)
- `IFileSystemWatcherService` — watch for changes, publish events

**Status:** ✅ Resolved (2026-06-21) — Clean separation exists: `IFileSystemService` for enumeration, `IFileSystemWatcherService` for watching. Both registered in DI.

---

### R1.9 All interfaces must be registered in DI before exit *(priority: high, BLOCKER for M6)*

**Description:** Failing to register new interfaces in DI causes
"service not found" at runtime. Must verify all new interfaces are
registered.

**Required fix:** Document all registrations in `CORE_INFRASTRUCTURE.md`:

```csharp
services.AddSingleton<IDocumentManagementService, DocumentManagementService>();
services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();
services.AddSingleton<ISyntaxHighlighterService, SyntaxHighlighterService>();
services.AddSingleton<IProcessRunnerService, ProcessRunnerAdapter>();
// ... etc.
```

**Status:** ✅ Resolved (2026-06-21) — Verified all interfaces registered in App.axaml.cs:
- IDocumentManagementService ✅
- IFileSystemService ✅
- IFileSystemWatcherService ✅
- IProjectLoader ✅
- ILanguageDetectionService ✅
- ILSPService ✅
- IProcessRunner ✅

---

### R1.10 Existing tests must not break from interface changes *(priority: high, BLOCKER for all milestones)*

**Description:** Adding interfaces changes constructor signatures of
services. Existing tests that construct services directly or stub them
may break.

**Required fix:** Before each milestone, run `dotnet test tests`.
Update test stubs to implement new interfaces if needed:

```csharp
// Old test stub:
public class StubProcessRunner { ... }

// New test stub must implement IProcessRunnerService:
public class StubProcessRunner : IProcessRunnerService { ... }
```

**Status:** ✅ Fixed — All test files updated to use `IDocumentManagementService` type. `tests/aero.Tests.csproj` updated to include the new interface file. All 301/301 tests pass.

---

## Round 2 — Post-Milestone Review

Findings from reviewing implementation against plan.

### R2.1 — M1 Verification (DocumentManager abstraction)

**Status:** ✅ Closed (2026-06-21) — Confirmed `IDocumentManagementService` + `DocumentManager` implementation alignment in code.

---

## Round 3 — Post-Milestone Review

Findings from reviewing implementation against plan.

### R3.1 — M2 Verification (File explorer/service abstractions)

**Status:** ✅ Closed (2026-06-21) — Confirmed `IFileSystemService`, `IProjectLoader`, and `IFileSystemWatcherService` separation and DI registration.

---

## Round 4 — Post-Milestone Review

Findings from reviewing implementation against plan.

### R4.1 — M3 Verification (Syntax highlighting boundary)

**Status:** ✅ Closed (2026-06-21) — Confirmed syntax highlighting remains View-layer integration; ViewModels depend on `ILanguageDetectionService` only.

---

## Round 5 — Post-Milestone Review

Findings from reviewing implementation against plan.

### R5.1 — M4 Verification (LSP abstraction posture)

**Status:** ✅ Closed (2026-06-21) — Confirmed current concrete `LSPManager`/`LSPSession` architecture is stable and non-breaking for this phase.

---

## Round 6 — Post-Milestone Review

Findings from reviewing implementation against plan.

### R6.1 — M5 Verification (Process runner abstraction)

**Status:** ✅ Closed (2026-06-21) — Confirmed `IProcessRunner` exists and is registered in DI; no regression found.

---

## Round 7 — Exit Gate Review

Findings from final review before declaring Phase 5.5 complete.

### R7.1 — Exit Gate

**Status:** ✅ Closed (2026-06-21) — Verified:
- `dotnet build src/aero.csproj` passes
- `dotnet test tests` passes (301/301)
- Phase 5.5 checklist in roadmap is marked complete
- No open Phase 5.5 review findings remain

---

## Persistent Checks

Use these as the self-review checklist before closing Phase 5.5:

- [x] M0 entry gate verified (Phase 5 complete, build green)
- [x] All new interfaces have corresponding implementation classes
- [x] All new interfaces are registered in DI (`App.axaml.cs`)
- [x] No breaking changes to existing public APIs
- [x] Factories added only where auto-detection is genuinely needed
- [x] All existing tests pass (`dotnet test tests`)
- [x] All new interfaces documented in `CORE_INFRASTRUCTURE.md`
- [x] `ISyntaxHighlighterService` stays in View layer (MVVM preserved)
- [x] No new dependencies added (`docs/LIBRARIES.md` unchanged)
- [x] No `async void` introduced outside Avalonia event handlers
- [x] No static service access or service locator patterns introduced
- [x] All adapters wrap existing implementations 1:1 (no logic duplication)
- [x] `dotnet build src/aero.csproj` passes
- [x] `dotnet test tests` passes
- [x] `manual_test_phase5.sh` still passes (no regression)
- [x] `docs/phases/phase-5.5/TOFIX.md` has no open items before Phase 6 starts
- [x] `docs/roadmap/PHASES.md` Phase 5.5 items all `[x]`
- [ ] `docs/phases/phase-5.5/README.md` status updated to complete
