# Coding Conventions

Quick rules so all Aero code reads like one person wrote it.

## Naming

| Thing | Case | Example |
|-------|------|---------|
| Namespaces | PascalCase | `Aero.Services`, `Aero.Agent.Adapters` |
| Classes / Structs | PascalCase | `DocumentManager`, `AgentRouter` |
| Interfaces | PascalCase + `I` prefix | `IAgent`, `IPanel` |
| Methods | PascalCase | `OpenDocument()`, `GatherContext()` |
| Properties | PascalCase | `IsDirty`, `ActiveDocument` |
| private fields | `_camelCase` | `_documents`, `_isDisposed` |
| local vars | camelCase | `fileName`, `lineCount` |
| Constants | PascalCase | `MaxTabCount` |

## Files

- One class per file (exceptions: small related records/enums)
- File name = class name: `DocumentManager.cs`, `IAgent.cs`
- XAML files: `Foo.axaml` + `Foo.axaml.cs` code-behind (minimal logic only)

## Abstraction-First (IMPORTANT)

Every feature must be designed with abstraction in mind:

```csharp
// ❌ Bad: .NET only
class BuildService
{
    Task BuildAsync() => Process.Start("dotnet build");
}

// ✅ Good: Interface first
interface IBuildService
{
    string Name { get; }
    string ProjectFilePattern { get; }
    Task<BuildResult> BuildAsync(BuildOptions options, CancellationToken ct);
}

class DotNetBuildService : IBuildService { ... }
class NpmBuildService : IBuildService { ... }  // Future
```

**Rules:**
1. Define `I{Feature}Service` before implementation
2. Use factory for auto-detection
3. Add new implementations without rewriting core

## Namespaces

```csharp
// Matches folder structure
src/Services/DocumentManager.cs  →  namespace Aero.Services
src/Agent/Adapters/CliAdapter.cs →  namespace Aero.Agent.Adapters
```

## MVVM

- **ViewModels** never reference Views directly — only via data binding
- **Services** never reference ViewModels or Views
- **Models** are plain data objects — no logic, no INotifyPropertyChanged
- Use `MessageBus` for cross-cutting events (DocumentOpened, BuildFinished, etc.)

## Async

- Suffix async methods with `Async`: `Task OpenDocumentAsync()`
- Avoid `async void` except for Avalonia event handlers
- Use `CancellationToken` on any I/O-bound method

## Nullability

- Nullable reference types enabled project-wide (`<Nullable>enable</Nullable>`)
- Use `?` only when null is genuinely valid
- Prefer `?? throw new InvalidOperationException(...)` over `!` (null-forgiving)

## Formatting

- 4 spaces (see `.editorconfig`)
- Opening brace on a new line (matches `.editorconfig` / Allman style)
- `var` when type is obvious from right side: `var doc = new TextDocument();`
- Explicit type when it's not obvious: `string path = GetPath();`

## Commit Messages

```
area: short imperative summary

Optional body with details.
- Bullet points for changes
```

Examples: `editor: add undo/redo stack`, `agent: implement CliAgentAdapter`
