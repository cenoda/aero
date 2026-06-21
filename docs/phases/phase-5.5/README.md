# Phase 5.5: Abstraction Implementation Pass

> Implement abstraction-first design for completed phases. No new features — only adding interfaces and factories.

## Goal

Apply abstraction-first design to Phase 5 (Output Panel) and prepare for Phase 6+.

## Entry Condition

- Phase 5 complete (Output panel)

## Exit Condition

- IProcessRunnerService interface exists
- ProcessRunnerServiceFactory for auto-detection
- Ready for Phase 6 (Build)

## Architecture

### Interface

```csharp
public interface IProcessRunnerService
{
    string Name { get; }  // "dotnet", "npm", "git", etc.
    Task<ProcessResult> RunAsync(ProcessOptions options, CancellationToken ct);
    IAsyncEnumerable<string> StreamOutputAsync(CancellationToken ct);
}

public record ProcessOptions(
    string Command,
    string WorkingDirectory,
    IEnumerable<string>? Arguments = null
);

public record ProcessResult(
    bool Success,
    int ExitCode,
    string Output,
    string Error
);
```

### Factory

```csharp
public class ProcessRunnerServiceFactory
{
    public IProcessRunnerService? Detect(string command)
    {
        // Check for dotnet, npm, git, etc.
    }
}
```

## Checklist

- [ ] **IProcessRunnerService interface** — abstraction for process execution
- [ ] **ProcessRunnerServiceFactory** — auto-detect command type
- [ ] **Verify existing ProcessRunner** — refactor to use interface

## Related Documents

- `docs/phases/phase-5/README.md` — Output Panel (Phase 5)
- `docs/phases/phase-6/README.md` — Build System (Phase 6)
- `AGENTS.md` — Abstraction-First Design section

## Notes

- This phase implements abstraction for Phase 5's ProcessRunner
- Phase 6's BuildService will extend this pattern
- No new features — only interface/factory additions