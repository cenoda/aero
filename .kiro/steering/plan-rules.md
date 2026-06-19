# Phase Planning Convention

Every phase gets an implementation plan (`docs/phases/phase-N/IMPLEMENTATION_PLAN.md`)
before coding starts. These rules keep plans solid and stop them from drifting into
over-engineered, over-reviewed documents. They were distilled from the Phase 4 planning
cycle — read them before writing or reviewing any phase plan.

## 1. Verify against live code, not design docs

- Design/architecture docs (`docs/design/`, `docs/architecture/`) are aspirational and go
  stale. Never base a plan claim on them alone.
- Every claim about an existing seam must name the real file/method/record and be checked
  against current `src/`. If the plan says "reuse X," open X first.
- When a design doc and the code disagree, the code wins. Note the stale doc as a fix item.

## 2. Verify external APIs before depending on them

- Before the plan commits to a library/tool API, confirm it exists in the actually-restored
  version (inspect the assembly, the package, or a quick spike) — not from memory.
- Confirm external binaries/tools the phase needs are installable/present, and state that as
  an entry prerequisite. A plan that assumes an unverified API is not ready.

## 3. Build for this phase only (YAGNI)

- Implement what the current phase's stated scope requires — nothing for a future phase.
- Do **not** add interfaces, abstractions, or extra services because a later phase "might
  need" them. Introduce the abstraction in the phase that has the real second use.
- If the phase says "basic," basic wins. Scope creep is a planning bug.

## 4. Prefer documented limitations over edge-case code

- For a first/basic cut, an edge case (rare ordering, rename sync, etc.) may be **documented
  as a known limitation** instead of implemented.
- Each plan should carry a short "Phase N Limitations (by design)" section. Reach for it
  before adding logic to the critical path.

## 5. Make gates verifiable

- Entry (M0) and exit (Definition of Done) gates must be checkable commands/states:
  build passes, `dotnet test tests` passes (with the real current count), manual smoke passes,
  prior-phase `TOFIX.md` empty.
- "It should work" is not a gate.

## 6. Respect review diminishing returns

- Reviews catch real correctness and cross-phase issues. Past that point, extra rounds tend
  to *add* edge cases and complexity — the opposite of solid.
- Stop reviewing when a round produces only speculative or cosmetic items. Separate
  essential correctness work from gold-plating, and cut the gold-plating.

## 7. Record deliberate reductions

- When you cut or downgrade something on purpose, record it in `TOFIX.md` as a reduction
  with the rationale, marked "do not re-add without a concrete need."
- This stops a later review from silently reversing a deliberate simplification.

## 8. One concern per change

- Plans and the commits that implement them follow `AGENTS.md`: one feature/fix/refactor per
  change. Keep milestones small and independently verifiable.
