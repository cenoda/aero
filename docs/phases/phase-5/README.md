# Phase 5: Output Panel (Fake Terminal)

> See what your commands did.

## Goal

Run external commands and stream their output in a panel.

## Entry Condition

- Phase 4 complete (LSP integration)

## Exit Condition

- Can run commands (dotnet, git, etc.) and see stdout/stderr in real time
- Output panel toggles with Ctrl+`
- Commands can be cancelled mid-run
- Output is scrollable and copyable

## Checklist

- [ ] **ProcessRunner** — `CliWrap`으로 커맨드 실행 (dotnet, git 등)
- [ ] **Output panel** — stdout/stderr 실시간 스트리밍
- [ ] Ctrl+` 로 패널 토글
- [ ] 실행 중 취소 버튼 (CancellationToken)

## Related Documents

- [`docs/phases/phase-5/IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) — full implementation plan
- [`docs/phases/phase-5/TOFIX.md`](TOFIX.md) — pre-implementation risks and persistent checks
- `docs/LIBRARIES.md` — CliWrap (Phase 5 only; Pty.Net/VtNetCore are Phase 9.5)
- `docs/architecture/IDE_CORE.md` — Terminal subsystem
- `docs/design/PANELS_AND_DOCKING.md` — Bottom panel layout

## Notes

- This is a "fake" terminal — text output only, no PTY, no interactive shell.
- Real terminal (PTY) is Phase 9.5 (optional, high difficulty).
- ProcessRunner should be reusable for Phase 6 (Build) and Phase 7 (Git).
- Output panel is a singleton (one instance, multiple command histories).
