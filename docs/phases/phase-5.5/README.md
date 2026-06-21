# Phase 5.5: Abstraction Implementation Pass

> Apply abstraction-first design to all completed phases (0–5), ensuring the
> architecture supports the multi-language IDE vision. No new features.

---

## 1. Entry Gate: Confirm Phase 5 Is Solid Before Crossing the Boundary

Do not write Phase 5.5 code until all of these are true:

| Gate | Evidence |
|------|----------|
| `docs/roadmap/PHASES.md` Phase 5 checklist all `[x]` | ✅ |
| `docs/phases/phase-5/TOFIX.md` all closed | ✅ |
| `dotnet build src/aero.csproj` succeeds with 0 errors | ✅ verified at M0 |
| `dotnet test tests` passes | ✅ verified at M0 |
| `./manual_test_phase5.sh` smoke test completes | ✅ verified at M0 |
| `docs/issues/INDEX.md` has no open blockers | ✅ verified at M0 |

**First Phase 5.5 file created:** `docs/phases/phase-5.5/TOFIX.md` — so every
review finding has a home before code lands.

---

## 2. Scope

### In Scope

Review and augment all existing services with interface-first patterns:

- **Phase 0 (Foundation):** Verify DI container uses interfaces for all services.
- **Phase 1 (Editor):** `IDocumentManagementService`, `ITextBufferService`.
- **Phase 2 (File Explorer):** `IFileSystemService`, `IProjectLoaderService`.
- **Phase 3 (Syntax Highlighting):** `ISyntaxHighlighterService`.
- **Phase 4 (LSP):** `ILSPService`, `ILSPClientService` (verify existing abstraction).
- **Phase 5 (Output):** `IProcessRunnerService`, `ProcessRunnerServiceFactory`.

Apply the standard interface + factory pattern to any service that lacks one.

### Out of Scope

- **No new features** — only interface/factory additions.
- **No rewriting existing implementations** — wrap/adapter pattern only.
- **No breaking changes** — existing public APIs remain stable.
- **No theme system changes** (Phase 8).
- **No build system implementation** (Phase 6).

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    DI Container (Microsoft.Extensions.DI)    │
│  ┌─────────────────────────────────────────────────┐   │
│  │ Singleton Services (registered via interfaces)    │   │
│  │  IMessageBus ──→ MessageBus                    │   │
│  │  IFileSystemService ──→ FileSystemService        │   │
│  │  IProjectLoaderService ──→ ProjectLoaderService   │   │
│  │  ILanguageDetectionService ──→ LangDetectSvc  │   │
│  │  ILanguageService ──→ LanguageService         │   │
│  │  ISyntaxHighlighter ──→ TextMateHighlighter  │   │
│  │  ILSPService ──→ LSPManager                  │   │
│  │  IProcessRunnerService ──→ ProcessRunner     │   │
│  └─────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────┐   │
│  │ Factory Classes (auto-detection)                │   │
│  │  FileSystemServiceFactory                       │   │
│  │  ProjectLoaderServiceFactory                  │   │
│  │  LanguageServiceFactory                       │   │
│  │  ProcessRunnerServiceFactory                  │   │
│  └───────────────────────���─────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Service Layers:**

| Layer | Responsibility |
|-------|---------------|
| Interface | Abstraction boundary (testable, swappable) |
| Implementation | Concrete logic |
| Factory | Auto-detection for runtime selection |
| Consumer | Receives interface via DI, never concrete types |

---

## 4. Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| ADR-1 | All public services must have interfaces | Enables mocking in tests, future swapping. Matches AGENTS.md §4. |
| ADR-2 | Use factory pattern for auto-detection | Allows runtime selection based on project type, workspace, or detected tools. |
| ADR-3 | No breaking changes to existing APIs | Wrap/concatenate instead of replace. Consumers see no difference. |
| ADR-4 | Keep implementations as concrete classes | Don't force full abstraction on simple implementations. Interfaces are the boundary. |
| ADR-5 | No new NuGet packages | Uses existing DI, no new dependencies. |
| ADR-6 | Register interfaces, not implementations in DI | Consumer code depends on abstraction. |

---

## 5. Component Design

### 5.1 Interface Pattern

Every service follows this structure:

```csharp
// src/Services/I{Feature}Service.cs
namespace Aero.Services;

public interface I{Feature}Service
{
    /// <summary>Human-readable name for debugging/logging.</summary>
    string Name { get; }

    /// <summary>Execute the service operation.</summary>
    Task<{Feature}Result> ExecuteAsync({Feature}Options options, CancellationToken ct);
}
```

### 5.2 Factory Pattern

```csharp
// src/Services/{Feature}ServiceFactory.cs
namespace Aero.Services;

public static class {Feature}ServiceFactory
{
    /// <summary>Detect and create appropriate service for the workspace.</summary>
    public static I{Feature}Service? Detect(string workspacePath)
    {
        // Auto-detect project type, available tools, etc.
        // Return null if no appropriate service found
    }
}
```

### 5.3 Phase-by-Phase Details

#### Phase 0: Foundation

| Component | Status | Action |
|-----------|--------|--------|
| `IMessageBus` | ✅ exists | Verify registration |
| `MessageBus` | ✅ exists | Verify interface usage |
| DI registration | ✅ exists | Document in `CORE_INFRASTRUCTURE.md` |

**Verification:** Confirm all services registered via `services.AddSingleton<I..., Concrete>()`.

#### Phase 1: Editor

| Component | Status | Action |
|-----------|--------|--------|
| DocumentManager | works, no interface | Add `IDocumentManagementService` |
| TextDocument | AvaloniaEdit (concrete) | Add `ITextBufferService` abstraction |

**New Interfaces:**
```csharp
public interface IDocumentManagementService
{
    string Name { get; }
    Task<TextDocument> OpenAsync(string path, CancellationToken ct);
    Task SaveAsync(TextDocument doc, CancellationToken ct);
    // ... existing methods wrapped
}
```

#### Phase 2: File Explorer

| Component | Status | Action |
|-----------|--------|--------|
| `IFileSystemService` | ✅ exists | Verify factory needed |
| `IProjectLoader` | ✅ exists | Verify factory needed |
| FileSystemWatcherService | works, no interface | Add `IFileSystemWatcherService` |

**Existing interfaces:**
- `IFileSystemService` — verify `FileSystemServiceFactory` not needed (singleton pattern works)
- `IProjectLoader` — add `ProjectLoaderServiceFactory` for multi-project-type detection

**New:**
```csharp
public interface IFileSystemWatcherService
{
    string Name { get; }
    IObservable<FolderChanged> FolderChanged { get; }
    void Watch(string path);
    void Stop();
}
```

#### Phase 3: Syntax Highlighting

| Component | Status | Action |
|-----------|--------|--------|
| `ILanguageDetectionService` | ✅ exists | No action needed |
| TextMate in View | concrete | Add `ISyntaxHighlighterService` |

**New Interface:**
```csharp
public interface ISyntaxHighlighterService
{
    string Name { get; }
    void Install(TextEditor editor, string languageId);
    void SetLanguage(string languageId);
    void Dispose();
}
```

#### Phase 4: LSP Integration

| Component | Status | Action |
|-----------|--------|--------|
| `ILSPService` | ✅ exists | Verify in `CORE_INFRASTRUCTURE.md` |
| LSPManager | works | Add `ILSPClientService` factory |

**Existing:** `ILSPService` already abstracted.

**New:**
```csharp
public interface ILSPClientService
{
    string Name { get; }
    Task<ILSPService> CreateForDocumentAsync(TextDocument doc, CancellationToken ct);
}
```

#### Phase 5: Output Panel

| Component | Status | Action |
|-----------|--------|--------|
| ProcessRunner | works, no interface | Add `IProcessRunnerService` |
| CliWrap usage | concrete | Factory for command type |

**New Interface:**
```csharp
public interface IProcessRunnerService
{
    string Name { get; }
    Task<ProcessResult> RunAsync(ProcessOptions options, CancellationToken ct);
    IAsyncEnumerable<string> StreamOutputAsync(CancellationToken ct);
    event Action<string>? OutputReceived;
    void Cancel();
}
```

**Factory:**
```csharp
public static class ProcessRunnerServiceFactory
{
    public static IProcessRunnerService? Detect(string workspacePath)
    {
        // Check for dotnet, npm, git, python, etc.
        // Return appropriate runner
    }
}
```

---

## 6. File & Folder Layout

| Path | Action | Purpose |
|------|--------|---------|
| `src/Services/IDocumentManagementService.cs` | new | Editor abstraction |
| `src/Services/DocumentManagementService.cs` | new | Wrap existing DocumentManager |
| `src/Services/IFileSystemWatcherService.cs` | new | File watcher abstraction |
| `src/Services/FileSystemWatcherService.cs` | new | Wrap existing watcher |
| `src/Services/ISyntaxHighlighterService.cs` | new | TextMate abstraction |
| `src/Services/ProcessRunnerServiceFactory.cs` | new | Auto-detect command type |
| `src/Languages/ILanguageService.cs` | new | Language operations abstraction |
| `src/Languages/LanguageServiceFactory.cs` | new | Multi-language detection |
| `src/Languages/ILSPClientService.cs` | new | LSP client factory |
| `src/Languages/LSPClientServiceFactory.cs` | new | LSP session factory |
| `docs/phases/phase-5.5/TOFIX.md` | created | Phase 5.5 quality checklist |
| `docs/phases/phase-5.5/PROJECT_PLAN.md` | this file | Committed plan |
| `docs/architecture/CORE_INFRASTRUCTUREURE.md` | modify | Document all new interfaces |
| `docs/roadmap/PHASES.md` | modify | Mark Phase 5.5 items `[x]` |

---

## 7. Milestone Plan (Solid-State Sprints)

Each milestone ends with **`dotnet build src/aero.csproj` + `dotnet test tests` +
a short `dotnet run` smoke**. If a gate fails, fix before continuing.

### M0 — Entry Gate
- Verify Phase 5 gates from §1 (build, tests, manual smoke, no open blockers).
- Confirm all existing services restore cleanly.

### M1 — Phase 0 + Phase 1 Abstraction
- Add `IDocumentManagementService` + adapter.
- Verify `IMessageBus` registration.
- **Gate:** All Phase 1 tests pass; build green.

### M2 — Phase 2 Abstraction
- Add `IFileSystemWatcherService` interface.
- Add `ProjectLoaderServiceFactory` for multi-project detection.
- Document all Phase 2 services in `CORE_INFRASTRUCTURE.md`.
- **Gate:** All Phase 2 tests pass; build green.

### M3 — Phase 3 Abstraction
- Add `ISyntaxHighlighterService` interface.
- Add `LanguageServiceFactory`.
- **Gate:** All Phase 3 tests pass; highlighting still works.

### M4 — Phase 4 Abstraction
- Document `ILSPService` in `CORE_INFRASTRUCTURE.md`.
- Add `ILSPClientService` factory.
- **Gate:** All Phase 4 tests pass; LSP still works.

### M5 — Phase 5 Abstraction
- Add `IProcessRunnerService` interface + adapter for existing ProcessRunner.
- Add `ProcessRunnerServiceFactory`.
- Verify output panel still works with new interface.
- **Gate:** All Phase 5 tests pass; output panel functional.

### M6 — Exit Gate
- Full regression: Phase 0–5 automated + manual tests still pass.
- Record any review findings in `TOFIX.md` and close them.
- Update `docs/roadmap/PHASES.md` and the phase-5.5 status.
- **Gate:** `dotnet test` passes, app runs, TOFIX empty.

---

## 8. Testing Strategy

### 8.1 Unit Tests
- Each new interface has a corresponding mock in `tests/Stubs/`.
- Factories return appropriate mocks based on workspace detection.

### 8.2 Regression Tests
- Run the full `dotnet test tests` suite after every milestone. Existing tests (0–5) must keep passing.
- Keep public surface APIs stable.

### 8.3 Manual Smoke Test
Reuse existing `manual_test_phase5.sh` smoke test after M5 verification.

---

## 9. Solid-State Safeguards

| Safeguard | How it is enforced |
|-----------|-------------------|
| **Additive changes only** | Only *add* interfaces; wrap existing implementations. |
| **No breaking changes** | Existing consumers see no difference; DI registers both. |
| **MVVM boundary preserved** | Interfaces stay in Services layer, not ViewModels. |
| **Factory returns null on no-match** | Never throw; graceful degradation. |
| **No new dependencies** | Uses existing DI containers only. |
| **No async void** | All async methods return Task or Task<T>. |

---

## 10. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Factory detection returns wrong type | Medium | Wrong service used | Test each detection path (dotnet, npm, git). |
| Interface mismatch with existing API | Low | Breaking changes | Wrap existing methods 1:1; don't refactor. |
| DI registration conflict | Low | Runtime error | Register interface before implementation. |
| Too many factories | Low | Complexity creep | Only add where auto-detect needed. |

---

## 11. Documentation & Commit Plan

### Docs to Update
- `docs/phases/phase-5.5/TOFIX.md` — keep current; add findings per review round.
- `docs/phases/phase-5.5/PROJECT_PLAN.md` — this file (committed plan).
- `docs/roadmap/PHASES.md` — mark Phase 5.5 items `[x]` as milestones land.
- `docs/architecture/CORE_INFRASTRUCTURE.md` — document all new DI registrations.

### Suggested Commit Sequence
```
services: add IDocumentManagementService abstraction
services: add IFileSystemWatcherService abstraction
services: add ISyntaxHighlighterService abstraction
services: add IProcessRunnerService abstraction
docs: add Phase 5.5 factories and update architecture docs
```

---

## 12. Exit Criteria

Phase 5.5 is complete when **all** of the following are true:

- [ ] `docs/roadmap/PHASES.md` Phase 5.5 checklist is fully `[x]`.
- [ ] `dotnet build src/aero.csproj` succeeds with 0 errors.
- [ ] `dotnet test tests` passes (Phase 0–5 + new Phase 5.5 tests).
- [ ] `dotnet run --project src` launches; all features work.
- [ ] All new interfaces documented in `CORE_INFRASTRUCTURE.md`.
- [ ] `manual_test_phase5.sh` completes successfully.
- [ ] `docs/phases/phase-5.5/TOFIX.md` has no open items.
- [ ] No regressions in Phase 0–5 features.

---

## 13. One Recommended Path Forward

A single, conservative approach: work phase-by-phase from 0 to 5, adding
interfaces and minimal wrappers where they don't exist, and factories where
auto-detection is needed. Verify each milestone before moving to the next.

If approved, the first concrete actions are:
1. Verify the M0 entry gate (build + tests + manual smoke).
2. Implement M1 (`IDocumentManagementService` + wrap DocumentManager).
3. Checkpoint after M1 before adding Phase 2 abstractions.