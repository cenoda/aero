# Phase 5.5 — Implementation Plan

> **Phase:** 5.5 — Abstraction Implementation Pass
> **Date:** 2026-06-21
> **Status:** Ready for implementation

---

## 1. Goal

Apply abstraction-first design to all completed phases (0–5, plus Phase 4 LSP), ensuring the architecture supports the multi-language IDE vision. No new features — only interface and factory additions that wrap existing implementations.

Key deliverables:

- `IDocumentManagementService` — ✅ Already complete (M1)
- `IFileSystemService` — ✅ Already registered
- `IFileSystemWatcherService` — ✅ Already registered
- `IProjectLoader` — ✅ Already registered
- `ILanguageDetectionService` — ✅ Already registered
- `ILSPService` — Already exists, document in CORE_INFRASTRUCTURE.md
- `ISyntaxHighlighterService` — Add interface for Phase 3
- `IProcessRunner` — Already registered

---

## 2. Entry Check

Phase 5.5 may begin because:

| Gate | Evidence |
|------|----------|
| Phase 5 checklist complete | `docs/roadmap/PHASES.md` Phase 5 items all `[x]` |
| `docs/phases/phase-5/TOFIX.md` all closed | All items resolved |
| `dotnet build src/aero.csproj` succeeds | 0 errors |
| `dotnet test tests` passes | 301/301 |
| No Phase 5 blockers | Verified at M0 |

---

## 3. Scope

### In Scope

Review and augment all existing services with interface-first patterns:

- **Phase 0 (Foundation):** Verify DI container uses interfaces for all services.
- **Phase 1 (Editor):** `IDocumentManagementService` ✅, `ITextBufferService`.
- **Phase 2 (File Explorer):** `IFileSystemService` ✅, `IProjectLoader` ✅, `IFileSystemWatcherService` ✅.
- **Phase 3 (Syntax Highlighting):** `ILanguageDetectionService` ✅, add `ISyntaxHighlighterService`.
- **Phase 4 (LSP):** `ILSPService` (document existing), add `ILSPClientService` factory.
- **Phase 5 (Output):** `IProcessRunner` ✅.

### Out of Scope

- No new features
- No rewriting existing implementations (wrap/adapter only)
- No breaking changes to public APIs
- No theme system (Phase 8)
- No build system (Phase 6)

---

## 4. Milestone Plan

### M0 — Entry Gate ✅ DONE
- Verify Phase 5 gates from §2
- Confirm all existing services restore cleanly

### M1 — Phase 0 + Phase 1 Abstraction ✅ DONE
- `IDocumentManagementService` + adapter
- Verify `IMessageBus` registration
- **Gate:** All Phase 1 tests pass; build green

### M2 — Phase 2 Abstraction ✅ DONE
- `IFileSystemWatcherService` interface
- Document all Phase 2 services in `CORE_INFRASTRUCTURE.md`
- **Gate:** All Phase 2 tests pass; build green

### M3 — Phase 3 Abstraction (NEXT)
- Add `ISyntaxHighlighterService` interface
- Wrap existing TextMate integration
- Document in `CORE_INFRASTRUCTURE.md`
- **Gate:** All Phase 3 tests pass; highlighting still works

### M4 — Phase 4 Abstraction
- Document `ILSPService` in `CORE_INFRASTRUCTURE.md`
- Add `ILSPClientService` factory
- **Gate:** All Phase 4 tests pass; LSP still works

### M5 — Phase 5 Abstraction
- Verify `IProcessRunner` interface pattern
- Document in `CORE_INFRASTRUCTURE.md`
- **Gate:** All Phase 5 tests pass; output panel functional

### M6 — Exit Gate
- Full regression: Phase 0–5 tests pass
- Update `docs/roadmap/PHASES.md`
- **Gate:** `dotnet test` passes, app runs

---

## 5. Current TOFIX.md Status

| Item | Priority | Status |
|------|----------|--------|
| R1.1 — Method signature preservation | Critical | ✅ Resolved |
| R1.2 — DI registration conflict | High | ✅ Resolved |
| R1.3 — Factory pattern | Medium | ✅ Resolved |
| R1.4 — ISyntaxHighlighterService leak | High | Open |
| R1.5 — ILanguageService duplication | Medium | Open |
| R1.6 — ProcessRunner interface | Low | Open |
| R1.7 — ILSPClientService conflict | Medium | Open |
| R1.8 — IFileSystemWatcherService duplication | Low | Open |
| R1.9 — DI registration | High | ✅ Resolved |

---

## 6. Next Steps

Proceed to **M3 — Phase 3 Abstraction**:

1. Review existing TextMate/syntax highlighting code in `src/Languages/`
2. Create `ISyntaxHighlighterService` interface
3. Wrap existing implementation
4. Register in DI
5. Verify tests pass
6. Update `CORE_INFRASTRUCTURE.md`