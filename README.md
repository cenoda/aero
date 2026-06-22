# Aero IDE

**A general-purpose IDE built from scratch with abstraction-first design.**

**Aero** stands for **Agent Environment for Rapid Orchestration**.

Aero is built from scratch in C# with Avalonia. Think of it as VS Code but built from
scratch — a strong standalone editor that supports multiple languages, with
multi-agent orchestration designed into the architecture rather than bolted on later.

The IDE is designed to be language-agnostic from the start. Features are abstracted
so new languages can be added without rewriting core logic. Users can "turn off" unused
features in settings.

---

## Vision

| Traditional IDE | Aero |
|----------------|------|
| IntelliJ (one language = one product) | One editor = many languages |
| Install plugins for each language | Built-in multi-language support |
| Can't turn off unused features | Turn off unused features in settings |
| .NET-specific or JS-specific | Language-agnostic core |

---

## Features

### ✅ What Works Today (Phase 7 Complete)
- Tabbed text editor with AvaloniaEdit (syntax-aware, line numbers)
- File open/save via Ctrl+O / Ctrl+S and system dialogs
- Undo/Redo (full AvaloniaEdit undo stack)
- Find/Replace with inline overlay panel (Ctrl+F)
- Status bar with cursor position (Ln X, Col Y)
- Dirty document tracking with save-on-close prompts
- File Explorer panel with tree view (Phase 2)
- Syntax highlighting via TextMate grammars (Phase 3)
- LSP integration for diagnostics and completions (Phase 4)
- Problems panel with diagnostics list
- Ctrl+Space for code completion
- Output panel with process runner (Phase 5)
- Multi-language build system with Ctrl+Shift+B (Phase 6)
- Build errors populate Problems panel; click to jump to file/line
- Git panel with staged/unstaged changes, diff viewer, commit UI (Phase 7)
- Branch indicator in status bar
- File modified indicators in editor tabs and file tree
- Branch graph with commit history visualisation

### 🚧 In Progress
- Phase 8: UI Polish (dockable panels, theme engine, command palette, welcome page, settings, workspace persistence)

### 🧠 The Agent Layer (Coming After Phase 8)
- Multiple AI agents running side-by-side
- Shared workspace context (open files, cursor, diagnostics, git diff)
- Agent-to-agent task relay pipelines
- Chat panel with streaming responses
- "Apply to editor" for AI-generated code

[Full roadmap →](docs/roadmap/PHASES.md)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | [Avalonia 11.3](https://avaloniaui.net/) (XAML) |
| Language | C# (.NET 9.0) |
| Architecture | MVVM with [ReactiveUI](https://www.reactiveui.net/) |
| Text Editor | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Event Bus | Custom `MessageBus` (record-based) |

### Abstraction-First Design

Aero uses interface-first design for extensibility:

```csharp
// Build system example
interface IBuildService
{
    string Name { get; }
    string ProjectFilePattern { get; }
    Task<BuildResult> BuildAsync(BuildOptions options, CancellationToken ct);
}

class DotNetBuildService : IBuildService { ... }  // .NET
class NpmBuildService : IBuildService { ... }    // Future: Node.js
class CargoBuildService : IBuildService { ... } // Future: Rust
```

This pattern applies to all features: syntax highlighting, LSP, Git, etc.

---

## Quick Start


### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **C# LSP server** (for diagnostics and completions — Phase 4 feature):
  ```bash
  dotnet tool install --global csharp-ls
  ```
  Verify installation with `csharp-ls --version`. Aero uses `csharp-ls` as the C# language server.
  If `csharp-ls` is unavailable, the editor still works — LSP features are silently disabled
  and a status-bar message is shown.

### Build & Run

```bash
git clone https://github.com/your-org/aero.git
cd aero
dotnet run --project src
```

---

## Architecture

```
┌──────────────────────────────────────────────┐
│         AERO IDE (Standalone)                │
│  Editor │ Tabs │ FileTree │ Terminal │ Build │
│  Git │ Search │ LSP │ Settings │ Plugins     │
├──────────────────────────────────────────────┤
│       AGENT ORCHESTRATION LAYER              │
│  Router │ Registry │ Context Injection       │
│  CLI Adapters │ API Adapters │ Local Models  │
└──────────────────────────────────────────────┘
```

The IDE is fully functional without agents. Agents are plugins.

→ [Architecture docs](docs/architecture/OVERVIEW.md)

---

## Development Status

**Current phase: Phase 8 — UI Polish (In Progress)**

| Phase | Status |
|-------|--------|
| 0 — Foundation | ✅ Complete |
| 1 — Editor | ✅ Complete |
| 2 — File Explorer | ✅ Complete |
| 3 — Syntax Highlighting | ✅ Complete |
| 4 — LSP Integration | ✅ Complete |
| 5 — Output Panel | ✅ Complete |
| 5.5 — Abstraction Pass | ✅ Complete |
| 6 — Multi-language Build | ✅ Complete |
| 7 — Git Integration | ✅ Complete |
| 8 — UI Polish | 🔄 In Progress |
| 9 — Advanced Features | ⬜ Planned |
| 10 — Plugin System | ⬜ Planned |
| A1–A5 — Agent Layer | ⬜ After Phase 8 |

[Full checklist →](docs/roadmap/PHASES.md)

### ✅ Completed
- Phase 5.5: Abstraction Implementation Pass
- Phase 6: Build & Output Integration
- Phase 7: Git Integration (branch graph, diff viewer, commit UI, auto-reload)

---

## Documentation

- [Architecture Overview](docs/architecture/OVERVIEW.md)
- [IDE Core Architecture](docs/architecture/IDE_CORE.md)
- [Core Infrastructure (MVVM, DI, MessageBus)](docs/architecture/CORE_INFRASTRUCTURE.md)
- [Agent Orchestration Design](docs/architecture/AGENT_ORCHESTRATION.md)
- [Coding Conventions](docs/CONVENTIONS.md)
- [Library Catalog](docs/LIBRARIES.md)
- [Development Roadmap](docs/roadmap/PHASES.md)

---

## License

[WTFPL](LICENSE) — Do what the fuck you want.
