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

## Architecture (Abstraction-First)

Phase 5 uses CliWrap for process execution. For abstraction-first design,
see Phase 5.5 which adds `IProcessRunnerService` interface.

```csharp
// Phase 5.5 adds this interface:
public interface IProcessRunnerService
{
    string Name { get; }
    Task<ProcessResult> RunAsync(ProcessOptions options, CancellationToken ct);
    IAsyncEnumerable<string> StreamOutputAsync(CancellationToken ct);
}
```

## Checklist

- [x] **ProcessRunner** — execute commands via `CliWrap` (dotnet, git, etc.)
- [x] **Output panel** — real-time stdout/stderr streaming
- [x] Toggle panel with Ctrl+`
- [x] Cancel button during execution (CancellationToken)

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
