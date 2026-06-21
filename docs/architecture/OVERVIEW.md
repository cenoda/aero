# Aero IDE — Architecture Overview

Aero is a **general-purpose IDE** (editor, tabs, terminal, git, build)
with a **multi-agent AI orchestration layer** as its killer feature.

Unlike traditional IDEs (IntelliJ = one language per product), Aero is designed
to be language-agnostic from the start using abstraction-first design.

**Core Principles:**
- **Interface first** — every feature has an abstraction layer
- **Auto-detection** — detect project type and enable appropriate services
- **Extensible** — add new languages without rewriting core
- **Disable unused** — turn off features not needed in settings

**See also:**
- `IDE_CORE.md` — the standalone IDE subsystems
- `AGENT_ORCHESTRATION.md` — multi-agent routing, adapters, context injection
- `CORE_INFRASTRUCTURE.md` — MessageBus, DI, MVVM patterns

---

## Two-Layer Architecture

```
┌──────────────────────────────────────────────┐
│              AERO IDE (Standalone)            │
│  Editor │ Tabs │ FileTree │ Terminal │ Build  │
│  Git │ Search │ LSP │ Settings │ Plugins      │
├──────────────────────────────────────────────┤
│         AGENT ORCHESTRATION LAYER             │
│  Router │ Registry │ Context Injection        │
│  CLI Adapters │ API Adapters │ Local Adapters │
│  Agent Panel │ Agent Chat │ Multi-Agent UI    │
└──────────────────────────────────────────────┘
```

The IDE works without agents. Agents are plugins that supercharge it.

---

## Tech Stack

| Layer       | Technology              |
|-------------|-------------------------|
| UI          | Avalonia 11.3 (XAML)    |
| Language    | C# (.NET 9.0)           |
| Pattern     | MVVM                     |
| Text Editor | AvaloniaEdit             |
| Agent Comm  | JSON-RPC (stdio), HTTP/SSE |

---

## High-Level UI Layout

```
+-----------+--------------------+-----------+
| Sidebar   |   Editor Area      |  Right    |
| .Explorer |   (tabs)           |  Panel    |
| .Agents   |                    |           |
| .Git      |   CODE HERE        |           |
+-----------+--------------------+-----------+
| Agent Chat / Terminal / Output / Problems  |
+--------------------------------------------+
|              Status Bar                     |
+--------------------------------------------+
```
