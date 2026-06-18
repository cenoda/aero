# Phase 2 — To Fix

> **Status:** Active — empty at plan approval. Add items as review findings surface. Resolve all open items before declaring Phase 2 complete.

Code quality issues found during Phase 2 review. Must be resolved before moving to Phase 3.

---

## Round 1 — Initial Review

*No findings yet.*

---

## Persistent Checks

Use these as a self-review checklist before closing Phase 2:

- [ ] No new NuGet packages were added without updating `docs/LIBRARIES.md`.
- [ ] All new services are registered in `src/App.axaml.cs` and documented in `docs/architecture/CORE_INFRASTRUCTURE.md`.
- [ ] All new public service methods are covered by unit tests.
- [ ] `FileSystemWatcherService` disposes its watcher and timer on stop/exit.
- [ ] `FileExplorerViewModel` unsubscribes from MessageBus in `Dispose()`.
- [ ] No `async void` outside Avalonia event handlers.
- [ ] No `!` null-forgiving operator without a comment explaining safety.
- [ ] Phase 1 regression tests still pass (`dotnet test tests`).
- [ ] Manual smoke test `manual_test_phase2.sh` passes.

---

## Issue Template

When adding a new finding, use this format:

```markdown
- [ ] **R#.X Short title** *(priority: low/medium/high/critical)*
  Description of what is wrong and why it matters.
  **Fix:** Concrete fix hint or approach.
```
