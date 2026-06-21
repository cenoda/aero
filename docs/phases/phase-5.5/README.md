# Phase 5.5: Refactoring Pass

> Clean up technical debt before continuing. No new features — only refactoring and documentation fixes.

## Goal

Apply abstraction-first design to all completed phases (0-5), ensuring the architecture supports multi-language IDE vision.

## Entry Condition

- Phase 5 complete (Output panel)

## Exit Condition

- All services follow abstraction-first pattern
- Documentation reflects abstraction-first design
- No .NET-specific assumptions in core architecture

## Why This Phase?

The agent track has been building features rapidly, but some implementations are .NET-specific:
- BuildService was hardcoded to `dotnet build`
- Other services may have similar issues

This phase ensures the foundation is solid before Phase 6+ adds more features.

## Checklist

### Documentation Updates

- [ ] **docs/phases/phase-0/README.md** — verify abstraction mentions
- [ ] **docs/phases/phase-1/README.md** — verify abstraction mentions
- [ ] **docs/phases/phase-2/README.md** — verify abstraction mentions
- [ ] **docs/phases/phase-3/README.md** — add ISyntaxHighlighterService abstraction
- [ ] **docs/phases/phase-4/README.md** — verify ILSPService abstraction
- [ ] **docs/phases/phase-5/README.md** — verify IProcessRunnerService abstraction
- [ ] **docs/phases/phase-6/README.md** — ✅ Already updated (abstraction-first)

### Architecture Review

- [ ] Review all `I*Service` interfaces for consistency
- [ ] Add factory classes where auto-detection is needed
- [ ] Document extension points for future implementations

### Code Review (if needed)

- [ ] Refactor any .NET-specific code to use interfaces
- [ ] Add placeholder implementations for future languages

## Related Documents

- `AGENTS.md` — Abstraction-First Design section
- `docs/architecture/OVERVIEW.md` — two-layer architecture