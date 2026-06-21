# Aero IDE — Core IDE Architecture (Standalone)

This document covers the IDE itself — the part that works without any AI agents.
For the agent orchestration layer, see `AGENT_ORCHESTRATION.md`.

## Core Principles (Abstraction-First)

Every feature is designed with abstraction in mind:

| Feature | Interface | Implementations |
|---------|-----------|-----------------|
| Build | `IBuildService` | DotNetBuildService, NpmBuildService, CargoBuildService |
| Syntax | `ISyntaxHighlighterService` | TextMateHighlighter, TreeSitterHighlighter |
| Git | `IGitService` | LibGit2SharpService, GitCliService |
| Terminal | `ITerminalService` | PtyTerminal, OutputPanel (Phase 5) |

This allows:
- Adding new languages without rewriting core
- Auto-detecting project type and enabling services
- Users can "turn off" unused features in settings

## Tech Stack

| Layer       | Technology              |
|-------------|-------------------------|
| UI          | Avalonia 11.3 (XAML)    |
| Language    | C# (.NET 9.0)           |
| Pattern     | MVVM (Model-View-ViewModel) |
| Text Editor | AvaloniaEdit            |
| LSP Client  | Custom / StreamJsonRpc  |

---

## High-Level Component Tree

```
Aero IDE
├── Shell (MainWindow)
│   ├── MenuBar
│   ├── ToolBar
│   ├── StatusBar
│   └── Workspace (dockable panel host)
│       ├── EditorPanel
│       │   ├── TabControl (open documents)
│       │   │   └── TextEditor (per file)
│       │   └── WelcomePage
│       ├── SidebarPanel (left)
│       │   ├── FileExplorer
│       │   ├── GitPanel
│       │   ├── AgentPanel        ← agent status & routing
│       │   └── OutlinePanel
│       ├── BottomPanel
│       │   ├── TerminalPanel
│       │   ├── OutputPanel
│       │   ├── ProblemsPanel
│       │   └── AgentChat          ← multi-agent conversation
│       └── RightPanel
│           └── PropertiesPanel
```

## Core Subsystems (agent-independent)

1. **Document Model** — open files, dirty tracking, text buffers
2. **Editor Core** — text rendering, syntax highlighting, undo/redo, search
3. **Language Services** — LSP integration (completion, diagnostics, hover)
4. **Project System** — file tree, project loader, file watcher
5. **Terminal** — integrated shell with pty
6. **Git Integration** — diff viewer, staged changes, commit UI
7. **Build System** — run builds, parse errors
8. **Settings** — user preferences, keybindings, themes
9. **Plugin System** — extension API (agents are plugins)
