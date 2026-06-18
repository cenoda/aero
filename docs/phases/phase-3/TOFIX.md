# Phase 3 — To Fix

> **Status:** Active — no review findings yet.
> Resolve all open items before declaring Phase 3 complete.
>
> This file is the persistent code-quality checklist for Phase 3 (Syntax
> Highlighting). Add findings here during/after every review round; mark each
> `[x]` when fixed and note the fix inline. Do not start Phase 4 while any item
> is unchecked. See `.kiro/steering/tofix-rules.md`.

---

## Round 1 — Plan Review

_No findings yet. Add items as the plan is reviewed._

---

## Persistent Checks

Use these as a self-review checklist before closing Phase 3:

- [ ] No new NuGet packages were added without updating `docs/LIBRARIES.md`
      (`AvaloniaEdit.TextMate` and `TextMateSharp.Grammars` are already referenced —
      confirm no others slipped in).
- [ ] All new services are registered in `src/App.axaml.cs` and documented in
      `docs/architecture/CORE_INFRASTRUCTURE.md`.
- [ ] All new public service methods are covered by unit tests.
- [ ] Language detection falls back to plain text for unknown extensions (no throw).
- [ ] TextMate installation is disposed/cleaned up with the editor control (no leak
      across tab open/close).
- [ ] No `async void` outside Avalonia event handlers.
- [ ] No `!` null-forgiving operator without a comment explaining safety.
- [ ] Phase 1 + Phase 2 regression tests still pass (`dotnet test tests`).
- [ ] Manual smoke test for syntax highlighting passes.
- [ ] `docs/phases/phase-3/TOFIX.md` has no open items.
