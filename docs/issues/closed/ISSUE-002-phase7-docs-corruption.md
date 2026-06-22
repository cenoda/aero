# Issue 002: Phase 7 Documentation Corruption

> **Status:** Open — pre-implementation documentation audit
> **Created:** 2026-06-22
> **Severity:** High — blocks Phase 7 implementation

---

## Description

Phase 7 planning documents contain **serious corruption** that falsely claims the phase is complete when no code exists. This appears to be injected garbage (possibly accidental or malicious) that violates the project's documentation conventions.

### Evidence of Corruption

| Document | Corrupted Claim | Reality |
|----------|----------------|---------|
| `docs/roadmap/PHASES.md` (lines 107-167) | Phase 7 marked `✅` complete with all items `[x]` | Phase 7 NOT STARTED (per `.memos/current-state.md`) |
| `docs/roadmap/PHASES.md` (lines 107-167) | Contains "Git rebase", "Git merge", "Git cherry-pick", etc. marked complete | These are **explicitly out of scope** per IMPLEMENTATION_PLAN §3/§10 |
| `docs/phases/phase-7/TOFIX.md` | R1.1, R1.2, R1.4, R1.5 marked `[x] Fixed` | Files `LibGit2SharpService.cs`, `GitServiceFactory.cs`, `GitViewModel.cs` **do not exist** |
| `docs/phases/phase-7/TOFIX.md` | "Persistent Checks" claim DI registered in `Program.cs` | DI is registered in `App.axaml.cs` (verified live code) |
| `docs/phases/phase-7/IMPLEMENTATION_PLAN.md` | Test baseline claims `337/337` | Actual count is `328/328` (verified via `dotnet test`) |
| `docs/phases/phase-7/IMPLEMENTATION_PLAN.md` | Claims `Repository.Stage()` / `Repository.Unstage()` exist | These instance methods were **removed** from modern LibGit2Sharp |

### Root Cause

The `docs/roadmap/PHASES.md` file contains **two contradictory Phase 7 sections**:

1. **First block (lines 107-167)** — CORRUPTED:
   - Marked `✅` complete
   - Contains massive duplication (Git merge/rebase/cherry-pick/reset/checkout/switch each appear 3+ times)
   - Includes items explicitly out of scope per IMPLEMENTATION_PLAN
   - Appears to be injected garbage

2. **Second block (lines 189-196)** — CORRECT:
   - All items `[ ]` unchecked
   - Matches the README.md 6-item checklist
   - This is the **actual** Phase 7 scope

---

## Debug Log

### Attempt 1: Verify Code Existence
- **Hypothesis:** Check if `src/Services/Git/` directory exists
- **Action:** `ls -la src/Services/`
- **Result:** No `Git/` subdirectory exists
- **Output:** Only contains `.gitkeep`, `Build/`, `DocumentManager.cs`, `FileSystemService.cs`, etc.

### Attempt 2: Verify DI Registration
- **Hypothesis:** Check where services are registered
- **Action:** Read `src/App.axaml.cs` and `src/Program.cs`
- **Result:** All DI registered in `App.axaml.cs::BuildServices()`, `Program.cs` has no DI
- **Output:** `Program.cs` is just a bootstrap entry point with `BuildAvaloniaApp()`

### Attempt 3: Verify Test Count
- **Hypothesis:** Confirm actual test count
- **Action:** `dotnet test tests --no-build`
- **Result:** 328/328 passing
- **Output:** `Passed! - Failed: 0, Passed: 328, Skipped: 0, Total: 328`

---

## Resolution

### Immediate Fixes Required

1. **Fix `docs/roadmap/PHASES.md`:**
   - Remove corrupted first Phase 7 block (lines 107-167)
   - Keep only the correct unchecked checklist (lines 189-196)
   - Ensure Phase 7 is marked as NOT STARTED

2. **Fix `docs/phases/phase-7/TOFIX.md`:**
   - Remove all false `[x] Fixed` claims for non-existent implementations
   - Mark all Round 1 items as `[ ] Open` or `[ ] Deferred`
   - Fix R1.11 to correctly state `App.axaml.cs` as DI location
   - Fix "Persistent Checks" to say `App.axaml.cs`

3. **Fix `docs/phases/phase-7/IMPLEMENTATION_PLAN.md`:**
   - Correct test baseline from 337 → 328
   - Update §5.2 to use `Commands.Stage()` / `Commands.Unstage()` instead of deprecated instance methods
   - Add note about verifying LibGit2Sharp native library on target platform

4. **Document the corruption:**
   - This issue file serves as the audit trail
   - Consider adding a note in `docs/CONVENTIONS.md` about documentation review process

### Prevention

- Add documentation review step to Phase completion checklist
- Require cross-reference verification (code ↔ docs ↔ tests) before marking phase complete
- Implement automated checks for documentation consistency (future Phase 8 feature)

---

## Related Files

- `docs/roadmap/PHASES.md` — corrupted Phase 7 checklist
- `docs/phases/phase-7/TOFIX.md` — false fix claims
- `docs/phases/phase-7/IMPLEMENTATION_PLAN.md` — wrong test count, outdated API
- `docs/phases/phase-7/README.md` — correct scope (no corruption)
- `.memos/current-state.md` — correct state (Phase 7 NOT STARTED)

---

## Resolution Note (2026-06-22)

All claims in this issue have been verified against current source code:

| Claim | Verdict | Evidence |
|-------|---------|----------|
| PHASES.md has two contradictory Phase 7 sections | ✅ **Resolved** — Single clean Phase 7 section at lines 143-154; no out-of-scope items | `grep` confirms no duplicate sections and no merge/rebase/cherry-pick items |
| `LibGit2SharpService.cs` etc. do not exist | ✅ **Resolved** — All 4 service files, 3 ViewModel files, 4 View files exist; total 1,413 lines of Git code | `wc -l src/Services/Git/*.cs src/ViewModels/Git*.cs src/Views/Git*.axaml` |
| DI registered in wrong location | ✅ **Resolved** — `App.axaml.cs:120` correctly registers `GitServiceFactory` and `GitViewModel` as singletons | Source code verified |
| Test baseline count wrong (337 vs 328) | ✅ **Resolved** — Current test count is 362/362, validating implementation | `dotnet test` confirmed |
| Wrong API (`Repository.Stage()` vs `Commands.Stage()`) | ✅ **Resolved** — All stage/unstage calls use `Commands.Stage(repo, path)` / `Commands.Unstage(repo, path)` | `LibGit2SharpService.cs` lines 129, 148 |

The corruption was pre-implementation documentation that was cleaned up as the actual code was written. The issue served its purpose as an audit trail and is now closed.

**Build:** `dotnet build src/aero.csproj` — ✅ 0 errors, 0 warnings
**Tests:** `dotnet test tests` — ✅ 362/362 passed

*Closed: 2026-06-22 | Status: Closed | Severity: High (resolved)*
