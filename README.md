# Aero IDE

**A standalone code editor with multi-agent AI orchestration as its killer feature.**

Built from scratch in C# with Avalonia. Think VS Code, but with multiple AI agents
(Cline, Copilot, local models) working together on your codebase — sharing context,
passing tasks between each other, and collaborating in real time.

The IDE works as a fully functional editor on its own. Agents are a layer on top
that supercharge it.

---

## Features

### ✅ What Works Today (Phase 1 Complete)
- Tabbed text editor with AvaloniaEdit (syntax-aware, line numbers)
- File open/save via Ctrl+O / Ctrl+S and system dialogs
- Undo/Redo (full AvaloniaEdit undo stack)
- Find/Replace with inline overlay panel (Ctrl+F)
- Status bar with cursor position (Ln X, Col Y)
- Dirty document tracking with save-on-close prompts

### 🚧 In Progress
- File Explorer panel with tree view (Phase 2)
- Syntax highlighting via TextMate grammars (Phase 3)
- LSP integration for diagnostics and completions (Phase 4)
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

**Current phase: Phase 2 (File Explorer & Project System)**

| Phase | Status |
|-------|--------|
| 0 — Foundation | ✅ Complete |
| 1 — Editor | ✅ Complete |
| 2 — File Explorer | 🚧 In Progress |
| 3 — Syntax Highlighting | ⬜ Planned |
| 4 — LSP Integration | ⬜ Planned |
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
