---
inclusion: always
---

# TOFIX Convention

Each phase has a `docs/phases/phase-N/TOFIX.md` file that tracks code quality issues found during review — things that must be resolved before moving to the next phase.

## Rules

- **Before starting work on a phase**, read its `TOFIX.md` and address any open items first.
- **After a review session**, add any new findings to `TOFIX.md` with a clear description and fix hint.
- **When an item is fixed**, mark it `[x]` and note the fix inline if non-obvious.
- **Do not move to the next phase** while any `TOFIX.md` item is unchecked.
- TOFIX is not a TODO (temporary) and not a formal issue — it is a persistent code quality checklist tied to the phase.
