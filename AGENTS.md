# Aero IDE — Agent Rules

> This file governs how AI agents work on the Aero IDE codebase.
> Read this before every task. Follow it strictly.

---

## 1. Project Context

**What this is:** A standalone code editor (IDE) built from scratch in C#/Avalonia, with a multi-agent AI orchestration layer as its differentiator.

**Tech Stack:**
- UI: Avalonia 11.3 (XAML)
- Language: C# (.NET 9.0)
- Pattern: MVVM with ReactiveUI
- Text Editor: AvaloniaEdit
- DI: Microsoft.Extensions.DependencyInjection
- Events: MessageBus (custom, record-based)

**Architecture:**
```
┌──────────────────────────────────────────────┐
│  IDE Core: Editor │ Tabs │ FileTree │ Terminal │
├──────────────────────────────────────────────┤
│  Agent Layer: Router │ Registry │ Adapters    │
└──────────────────────────────────────────────┘
```

**Key Documents:**
- `docs/roadmap/PHASES.md` — development phases and checklist
- `docs/architecture/OVERVIEW.md` — two-layer architecture
- `docs/architecture/CORE_INFRASTRUCTURE.md` — MVVM, DI, MessageBus patterns
- `docs/architecture/AGENT_ORCHESTRATION.md` — agent interfaces and routing
- `docs/CONVENTIONS.md` — coding conventions
- `docs/LIBRARIES.md` — NuGet packages and when to add them

---

## 2. Work Process Rules

### Phase Order (Strict)

Follow `docs/roadmap/PHASES.md` in order. Do not skip phases.
Do not start Agent Track (Phase A1+) until Phase 8 (UI Polish) is complete.

| Phase | Focus | Entry Condition |
|-------|-------|---------------|
| 0 | Foundation, DI, scaffold | — |
| 1 | Editor (buffer, tabs, open/save) | Phase 0 complete |
| 2 | File Explorer, Project System | Phase 1 complete |
| 3 | Syntax Highlighting | Phase 2 complete |
| 4 | LSP Integration | Phase 3 complete |
| 5 | Output Panel (process runner) | Phase 4 complete |
| 6 | Build & MSBuild integration | Phase 5 complete |
| 7 | Git Integration | Phase 6 complete |
| 8 | UI Polish (docking, theme, palette) | Phase 7 complete |
| 9 | Advanced Features | Phase 8 complete |
| 10 | Plugin System | Phase 9 complete |

### Before Starting Work

1. Read `docs/roadmap/PHASES.md` — confirm current phase and checklist
2. Read relevant architecture docs in `docs/architecture/`
3. Read `docs/CONVENTIONS.md`
4. Check `docs/LIBRARIES.md` — know which NuGet packages are available

### During Work

- **One concern per change.** A PR/commit should do one thing: add a feature, fix a bug, or refactor.
- **Update docs immediately** if you change architecture, conventions, or dependencies.
- **Update `docs/roadmap/PHASES.md` checklist** when completing items.
- **Add tests** for any new service, manager, or algorithmic logic.

### After Work

- **Build must pass:** `dotnet build src/aero.csproj` succeeds
- **Tests should pass for code changes:** `dotnet test tests` succeeds
- **Run/manual smoke when UI behavior changes:** use the relevant `manual_test_*.sh` script or `dotnet run --project src`
- **Commit message format:** `area: imperative summary`
  - Examples: `editor: add DocumentManager`, `lsp: implement LSPSession`
- **Update `docs/roadmap/PHASES.md`** — mark completed items with `[x]`

---

## 3. Coding Conventions (Enforced)

### Naming

| Thing | Case | Example |
|-------|------|---------|
| Namespaces | PascalCase | `Aero.Services`, `Aero.Agent.Adapters` |
| Classes / Structs | PascalCase | `DocumentManager`, `AgentRouter` |
| Interfaces | `I` + PascalCase | `IAgent`, `IPanel` |
| Methods | PascalCase | `OpenDocument()`, `GatherContext()` |
| Properties | PascalCase | `IsDirty`, `ActiveDocument` |
| private fields | `_camelCase` | `_documents`, `_isDisposed` |
| local vars | camelCase | `fileName`, `lineCount` |
| Constants | PascalCase | `MaxTabCount` |

### Files

- One class per file (exceptions: small related records/enums)
- File name = class name: `DocumentManager.cs`, `IAgent.cs`
- XAML: `Foo.axaml` + `Foo.axaml.cs` (code-behind minimal)

### Namespaces

Must match folder structure:
```
src/Services/DocumentManager.cs  →  namespace Aero.Services
src/Agent/Adapters/CliAdapter.cs →  namespace Aero.Agent.Adapters
```

### MVVM (Strict)

- **ViewModels** never reference Views directly — only data binding
- **Services** never reference ViewModels or Views
- **Models** are plain data — no `INotifyPropertyChanged`, no logic
- Use `MessageBus` for cross-cutting events (`DocumentOpened`, `BuildFinished`, etc.)
- ViewModels inherit `ReactiveObject`, use `[Reactive]` attribute

### Async

- Suffix async methods with `Async`: `Task OpenDocumentAsync()`
- Avoid `async void` except Avalonia event handlers
- Use `CancellationToken` on any I/O-bound method

### Nullability

- Project-wide nullable enabled (`<Nullable>enable</Nullable>`)
- Use `?` only when null is genuinely valid
- Prefer `?? throw new InvalidOperationException(...)` over `!` (null-forgiving)

### Formatting

- 4 spaces (see `.editorconfig`)
- Opening brace on a new line (matches `.editorconfig` / Allman style)
- `var` when type is obvious: `var doc = new TextDocument();`
- Explicit type when not obvious: `string path = GetPath();`

---

## 4. Abstraction-First Design (IMPORTANT)

**Aero is a general-purpose IDE, not .NET-specific.**

Every feature must be designed with abstraction in mind from the start.

### Why Abstraction?

| Without Abstraction | With Abstraction |
|-------------------|-----------------|
| IntelliJ (one language = one product) | VS Code (one editor = many languages) |
| Hard to add new languages | Easy to add new languages |
| "Turn off" not possible | "Turn off unused features" |

### Abstraction Rules

1. **Interface first** — Define `I{Feature}Service` before implementation
2. **Factory for detection** — Auto-detect project type and create service
3. **Extensible** — Add new implementations without rewriting core
4. **Disable unused** — Support "turn off unused features" in Phase 8 settings

### Examples

```csharp
// ❌ Bad: .NET only
class BuildService
{
    Task BuildAsync() => Process.Start("dotnet build");
}

// ✅ Good: Abstraction-first
interface IBuildService
{
    string Name { get; }
    string ProjectFilePattern { get; }
    Task<BuildResult> BuildAsync(BuildOptions options, CancellationToken ct);
}

class DotNetBuildService : IBuildService { ... }  // Phase 6
class NpmBuildService : IBuildService { ... }      // Future
class CargoBuildService : IBuildService { ... }    // Future
```

### When to Apply

Apply abstraction to:
- **Build System** (Phase 6) — ✅ Already abstracted
- **Syntax Highlighting** (Phase 3) — Future: tree-sitter, LSP grammar
- **Git** (Phase 7) — Future: LibGit2Sharp vs git CLI
- **Terminal** (Phase 9.5) — Future: PtyNet, vt100

---

## 5. Dependency Rules

### Library-First Principle

**Before implementing any non-trivial functionality, check if a library exists.**

| If you need... | Check for... | Before writing... |
|----------------|--------------|-------------------|
| Text editor widget | AvaloniaEdit | Custom text rendering |
| Syntax highlighting | AvaloniaEdit.TextMate + TextMateSharp.Grammars | Grammar parser |
| Dockable panels | Dock.Avalonia | Manual drag-drop layout |
| JSON-RPC / LSP | StreamJsonRpc | Custom protocol handler |
| Process execution | CliWrap | Process.Start boilerplate |
| Git operations | LibGit2Sharp | git CLI parser |
| Diff algorithm | DiffPlex | Custom diff logic |
| Fuzzy search | FuzzySharp | Manual string matching |
| Plugin loading | McMaster.NETCore.Plugins | AssemblyLoadContext |
| Reactive collections | DynamicData | ObservableCollection manual sync |
| Icons | Text glyphs for now; revisit icon library in Phase 8 | Custom icon assets |
| Modal dialogs | DialogHost.Avalonia | Custom overlay logic |

**Rule:** If a library in `docs/LIBRARIES.md` covers 80%+ of the need, use it.
Only build custom when:
- No library exists for the specific need
- The library is abandoned, buggy, or incompatible with .NET 9 / Avalonia 11.3
- The functionality is trivial (< 50 lines) and adding a dependency is overkill

**Process:**
1. Check `docs/LIBRARIES.md` — is there a library for this?
2. If yes, add it to `src/aero.csproj` and use it
3. If no, search NuGet briefly — is there a well-maintained alternative?
4. If still no, document the custom implementation in `docs/LIBRARIES.md` with "Custom — no suitable library found"

### Adding NuGet Packages

1. Check `docs/LIBRARIES.md` first — is this library already catalogued?
2. If not catalogued, add it to `docs/LIBRARIES.md` with: What It Does, Why You Want It, Phase
3. Add to `src/aero.csproj` with version matching existing patterns
4. Document any new DI registration in `docs/architecture/CORE_INFRASTRUCTURE.md`

### DI Registration

All services must be registered in `src/App.axaml.cs` (or a dedicated `ServiceCollection` extension):
```csharp
services.AddSingleton<IMessageBus, MessageBus>();
services.AddSingleton<DocumentManager>();
services.AddTransient<FileExplorerViewModel>();
```

Never use manual `new ServiceLocator()` or static service access.

---

## 5. Prohibited (Never Do)

| ❌ Prohibition | ✅ Instead |
|----------------|----------|
| Skip phases or start Agent Track early | Follow `PHASES.md` order strictly |
| ViewModels referencing Views directly | Data binding only |
| Services referencing ViewModels | Use MessageBus |
| `async void` (except event handlers) | Return `Task` |
| `!` null-forgiving operator | `?? throw new InvalidOperationException(...)` |
| Manual `ServiceLocator` | `Microsoft.Extensions.DependencyInjection` |
| Static service access | Constructor injection |
| One class doing multiple concerns | Split into focused classes |
| Commit without updating `PHASES.md` | Mark completed items immediately |
| Add library without documenting in `LIBRARIES.md` | Update catalog first |
| Start Phase 9.5 (real terminal) before everything else | It's optional, last resort |

---

## 6. Debug & Issue Rules

### Issue-First Debugging

**If a fix is not obvious after 2 attempts, create an issue file immediately.**

Do not keep debug attempts in chat history or memory only. Every attempt must be recorded.

**Trigger conditions:**
- `dotnet run` fails and the cause is not clear in 2 attempts
- A bug reappears after a previous "fix"
- Any behavior that contradicts `docs/architecture/` or `docs/CONVENTIONS.md`
- Uncertain whether a change is correct or safe

**Issue file location:** `docs/issues/open/ISSUE-###-short-name.md` (move to `docs/issues/closed/` when resolved)

**Issue file must contain:**
1. **Description** — what is wrong, expected vs actual
2. **Debug Log** — every attempt with:
   - Hypothesis (what I thought was wrong)
   - Action (what I tried)
   - Result (what happened)
   - Error / Output (exact message, stack trace, log)
3. **Resolution** — root cause, fix, commit hash, closed date (filled when done)

**Example Debug Log entry:**
```markdown
### Attempt 1
- **Hypothesis:** NullReferenceException in DocumentManager is caused by _activeDocument being null
- **Action:** Added null check before accessing _activeDocument.Path
- **Result:** Exception moved to line 47 instead of line 32
- **Error / Output:** `System.NullReferenceException: Object reference not set to instance at DocumentManager.OpenDocument() line 47`
```

### Issue Index Update

After creating or closing an issue, update `docs/issues/INDEX.md`:

```markdown
| 001 | Tab close crash | BUG | high | in-progress |
```

### No Silent Fixes

Never apply a fix without understanding why it works. If you don't know why it works, create an issue and document the uncertainty.

## 7. Decision Checkpoints (Stop and Ask)

Stop work and ask the user when:

1. **Architecture change** — modifying interfaces, DI setup, or MessageBus records
2. **New dependency** — adding a NuGet package not in `LIBRARIES.md`
3. **Phase boundary** — about to start a new Phase (confirm previous is complete)
4. **Skipping items** — want to skip a checklist item in `PHASES.md`
5. **Agent Track** — any work on Phase A1+ before Phase 8 is done
6. **Build failure** — `dotnet run` fails and you can't fix in 2 attempts
7. **Convention conflict** — existing code violates `CONVENTIONS.md` (don't refactor without asking)

---

## 8. Quick Reference

```bash
# Build and run
dotnet build src/aero.csproj
dotnet test tests
dotnet run --project src

# Check current phase status
cat docs/roadmap/PHASES.md | grep -E "^\s*- \[x\]|^\s*- \[ \]"

# Find where a service should be registered
grep -r "AddSingleton\|AddTransient\|AddScoped" src/App.axaml.cs src/
```

---

## 9. Lessons Learned from Phase 8.1a Failures

> **⚠️ This section was added after Phase 8.1a failed (v1), then updated after v2 also failed. All agents must read this before starting new implementation work.**

### What Happened

Phase 8.1a (Dockable Panels) was implemented using Dock.Avalonia library **twice** — once on `failed-dockable-panels` (v1) and again on `phase-8.1a-dockable-panels-v2` (v2, preserved as `failed-dockable-panels-v3`). Both attempts compiled successfully, all ViewModels were wired correctly, but the UI did not render panels as expected.

After 13+ debugging attempts per attempt, both implementations were reverted.

### Direction Change (2026-06-25)

**Dock.Avalonia is abandoned.** The library's internal rendering is opaque — code can be correct but the UI doesn't render as expected. The cost of debugging exceeds the value of the feature.

**Phase 8.1 is now "Panel Polish"** — polish the existing Grid+GridSplitter layout (sidebar + editor + bottom panel). This is the 95% use case. No drag-to-rearrange, no tear-away windows, no Dock.Avalonia.

### Key Lessons

#### 1. Validate Plan Before Implementation
- ❌ **Bad:** Create implementation plan → immediately write code
- ✅ **Good:** Create minimal proof-of-concept → verify library works → then implement

#### 2. Incremental Development
- ❌ **Bad:** Implement entire feature → test at end
- ✅ **Good:** One step at a time → test after each change → easy to identify what broke

#### 3. Understand the Library First
- ❌ **Bad:** Read docs → assume understand → write code
- ✅ **Good:** Read docs → create test app → verify behavior → then implement

#### 4. Debug-Friendly Code
- ❌ **Bad:** Write code → only add logging when debugging needed
- ✅ **Good:** Add logging from start → makes debugging possible

#### 5. Don't Rush
- ❌ **Bad:** "Just get it done" → fast but broken
- ✅ **Good:** Take time to verify → future debugging easier

#### 6. Know When to Walk Away (NEW)
- ❌ **Bad:** "Third time's the charm" → waste another 2 weeks
- ✅ **Good:** Two failed attempts = library is the problem, not you. Pivot.

### Post-Mortem Documents

See `/docs/POSTMORTEM-phase-8.1a.md` for v1 details.
See `failed-dockable-panels-v3` branch for v2 code.

---

*Last updated: 2026-06-25*
*Governs: entire project (`/home/cenoda/aero`)*
