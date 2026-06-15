# Aero IDE — Architecture Overview

Aero is a **usable standalone IDE** (editor, tabs, terminal, git, build)
with a **multi-agent AI orchestration layer** as its killer feature.

Multiple AI agents (Cline, Copilot, GPT-4, local models) run simultaneously,
sharing workspace context and passing tasks between each other.

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
| UI          | Avalonia 11.2 (XAML)    |
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
