# Aero IDE

**A fourth-generation IDE built from scratch for agent-native software development.**

**Aero** stands for **Agent Environment for Rapid Orchestration**.

Aero is built from scratch in C# with Avalonia. Think of it as an IDE designed for
the next generation of software development: a strong standalone editor first, with
multi-agent orchestration designed into the architecture rather than bolted on later.

The IDE is being built to work as a fully functional product on its own. The agent
layer comes after the editor foundation is solid, so orchestration features can sit
on top of a stable core instead of compensating for one.

---

## Features

### ✅ What Works Today (Phase 4 Complete)
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

### 🚧 In Progress
- Build system, Git integration, dockable panels, and more

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

**Current phase: Phase 4 (LSP Integration) — Complete**

| Phase | Status |
|-------|--------|
| 0 — Foundation | ✅ Complete |
| 1 — Editor | ✅ Complete |
| 2 — File Explorer | ✅ Complete |
| 3 — Syntax Highlighting | ✅ Complete |
| 4 — LSP Integration | ✅ Complete |
| 5 — Output Panel | ⬜ Planned |
| 6 — Build | ⬜ Planned |
| 7 — Git | ⬜ Planned |
| 8 — UI Polish | ⬜ Planned |
| 9 — Advanced Features | ⬜ Planned |
| 10 — Plugin System | ⬜ Planned |
| A1–A5 — Agent Layer | ⬜ After Phase 8 |

[Full checklist →](docs/roadmap/PHASES.md)

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
