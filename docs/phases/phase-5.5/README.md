# Phase 5.5: Abstraction Implementation Pass

> Review completed phases (0-5) and implement abstraction-first design. No new features.

## Goal

Apply abstraction-first design to all completed phases (0-5), ensuring the architecture supports multi-language IDE vision.

## Entry Condition

- Phase 5 complete (Output panel)

## Exit Condition

- All services follow interface-first pattern
- Factory classes exist for auto-detection
- Ready for Phase 6 (Build System)

## Scope: Phase 0-5 Review

### Phase 0: Foundation
- [ ] Verify DI container uses interfaces
- [ ] Check service registration follows patterns

### Phase 1: Editor
- [ ] Review DocumentManager for abstraction opportunities
- [ ] Verify TextBuffer abstraction (AvaloniaEdit)

### Phase 2: File Explorer
- [ ] Review IFileSystemService interface
- [ ] Check IProjectLoader abstraction

### Phase 3: Syntax Highlighting
- [ ] Add ISyntaxHighlighterService interface
- [ ] Add LanguageDetectionService factory

### Phase 4: LSP Integration
- [ ] Review ILSPService interface (already abstracted)
- [ ] Verify LSPManager uses interface

### Phase 5: Output Panel
- [ ] Add IProcessRunnerService interface
- [ ] Add ProcessRunnerServiceFactory

## Architecture Templates

### Interface Template

```csharp
public interface I{Feature}Service
{
    string Name { get; }
    Task<{Feature}Result> ExecuteAsync({Feature}Options options, CancellationToken ct);
}
```

### Factory Template

```csharp
public class {Feature}ServiceFactory
{
    public I{Feature}Service? Detect(string workspacePath)
    {
        // Auto-detect project type and return appropriate service
    }
}
```

## Checklist

- [ ] **Phase 0** — Verify DI and service registration
- [ ] **Phase 1** — Review DocumentManager
- [ ] **Phase 2** — Review IFileSystemService, IProjectLoader
- [ ] **Phase 3** — Add ISyntaxHighlighterService
- [ ] **Phase 4** — Verify ILSPService
- [ ] **Phase 5** — Add IProcessRunnerService
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