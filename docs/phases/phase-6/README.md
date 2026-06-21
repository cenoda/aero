# Phase 6: Build & Output

> Press a key. Build your project. See errors. Jump to them.

## Goal

Integrate build systems with the IDE using **abstraction-first** design for multi-language support.

## Status

✅ **Complete** (2026-06-21)

## Entry Condition

- Phase 5 complete (Output panel, ProcessRunner)

## Exit Condition

- [x] Ctrl+Shift+B runs the appropriate build command and streams output
- [x] Build errors are parsed and populate the Problems panel
- [x] Clicking an error jumps to file and line in the editor
- [x] Build success/failure is visible in status bar

## Architecture (Abstraction-First)

### Interface First

```csharp
// src/Services/Build/IBuildService.cs
public interface IBuildService
{
    // Build system identification
    string Name { get; }  // "dotnet", "npm", "cargo", etc.
    string ProjectFilePattern { get; }  // "*.csproj", "package.json", "Cargo.toml"
    
    // Build execution
    Task<BuildResult> BuildAsync(BuildOptions options, CancellationToken ct);
    IAsyncEnumerable<string> StreamOutputAsync(CancellationToken ct);
    
    // Error parsing (each build system has its own format)
    IEnumerable<ParsedError> ParseErrors(string output);
}

public record BuildOptions(
    string WorkingDirectory,
    bool IsCleanBuild = false,
    string? TargetFramework = null
);

public record BuildResult(
    bool Success,
    int ExitCode,
    TimeSpan Duration,
    IEnumerable<ParsedError> Errors
);

public record ParsedError(
    string FilePath,
    int Line,
    int Column,
    string Code,
    string Message,
    ErrorSeverity Severity
);
```

### Implementations

```
IBuildService (interface)
    │
    ├── DotNetBuildService    ← Phase 6: .NET projects (*.csproj, *.sln)
    ├── NpmBuildService    ← Future: Node.js (package.json)
    ├── CargoBuildService  ← Future: Rust (Cargo.toml)
    └── MakeBuildService  ← Future: C/C++ (Makefile)
```

### Factory (Auto-Detect)

```csharp
// src/Services/Build/BuildServiceFactory.cs
public class BuildServiceFactory
{
    public IBuildService? Detect(string workspacePath)
    {
        // Check for *.csproj, *.sln → DotNetBuildService
        // Check for package.json → NpmBuildService
        // Check for Cargo.toml → CargoBuildService
        // ...
    }
}
```

### Why Abstraction?

| Reason | Description |
|--------|-----------|
| **Multi-language** | Different languages have different build systems |
| **Extensibility** | Add new build systems without rewriting core |
| **User choice** | Auto-detect project type, user can override |
| **Disable unused** | "Turn off unused features" - disable build services not needed |

## Checklist

- [ ] **IBuildService interface** — abstraction with BuildCommand, ProjectFilePattern
- [ ] **BuildOptions / BuildResult models** — configuration and output
- [ ] **DotNetBuildService** — implements IBuildService for .NET
- [ ] **BuildServiceFactory** — auto-detect project type and create service
- [ ] **Output panel** — stream stdout/stderr (reuses Phase 5)
- [ ] Parse build error format → populate Problems panel
- [ ] Ctrl+Shift+B to build
- [ ] Click error in Problems → jump to file/line

## Related Documents

- `docs/architecture/IDE_CORE.md` — Build System subsystem
- `docs/LIBRARIES.md` — CliWrap (already in Phase 5)
- `docs/design/PANELS_AND_DOCKING.md` — Problems panel layout

## Notes

- MSBuild error format: `file(line,col): error CODE: message`
- npm error format: varies, but usually `error CODE in package.json`
- BuildService should be registered in DI; factory selects based on project type
- Problems panel already exists from Phase 4 (LSP diagnostics). Build errors append to it.
