# Agent Orchestration — Multi-Agent AI Layer

This is Aero's differentiator. The IDE works standalone. Agents plug in
and collaborate through a shared context bus.

## Concept

```
User types in Aero
        │
        ▼
┌───────────────────────────────────┐
│         AERO IDE (editor, tabs,   │
│         file tree, terminals...)   │
└───────────┬───────────────────────┘
            │ context (files, cursor, errors, git status)
            ▼
┌───────────────────────────────────┐
│        AGENT ROUTER               │
│  - receives user prompts          │
│  - gathers workspace context      │
│  - routes to best agent(s)        │
│  - passes results between agents  │
└───────┬───────┬───────┬───────────┘
        │       │       │
   ┌────▼──┐ ┌──▼───┐ ┌▼──────┐
   │Cline  │ │Copilot│ │GPT-4 │  ... more agents
   │(CLI)  │ │(API)  │ │(API)  │
   └───────┘ └───────┘ └───────┘
```

Agents don't talk to each other directly. They talk to the Router.
The Router broadcasts context, routes prompts, and merges results.

---

## Key Abilities

**1. Multiple agents active simultaneously** — Cline handles conversation
and planning while Copilot generates code in the background.

**2. Agent-to-agent handoff** — Cline decides "I need a React component" →
Router sends spec to Copilot → Copilot generates code → Router passes it
back to Cline → Cline reviews and applies to editor.

**3. Shared context injection** — Before any prompt, Router attaches:
currently open file + cursor, selected text, build errors, git diff,
project structure summary. No "can you read file X" back-and-forth.

**4. Frontal / backend split** — One agent (Cline) is the "face" talking
to the user. Others are "workers" generating code or running commands.
Cline delegates like a senior dev to a junior.


## Core Interfaces

### IAgent — every agent adapter implements this
```csharp
interface IAgent {
    string Id { get; }
    string Name { get; }          // "Cline", "Copilot", "GPT-4"
    AgentKind Kind { get; }       // CLI, API, Local
    AgentRole Role { get; }       // Frontend, Backend, Either

    Task<AgentResponse> SendAsync(AgentRequest req, CancellationToken ct);
    IAsyncEnumerable<AgentChunk> StreamAsync(AgentRequest req, CancellationToken ct);

    bool CanGenerateCode { get; }
    bool CanRunCommands { get; }
    bool CanReadFiles { get; }
}
```

### AgentRequest / AgentResponse
```csharp
class AgentRequest {
    string Prompt;
    WorkspaceContext Context;      // auto-injected by router
    string? SystemPrompt;          // optional override
    AgentRoutingHint Hint;         // "code-gen", "planning", "review"
}

class AgentResponse {
    string Content;
    List<CodeBlock> CodeBlocks;    // parsed ``` ``` blocks
    List<FileEdit> SuggestedEdits; // "create file X", "modify line Y"
    Dictionary<string, object> Metadata;
}

class WorkspaceContext {
    string? ActiveFilePath;
    string? ActiveFileContent;
    int CursorLine, CursorColumn;
    string? SelectedText;
    List<Diagnostic> Diagnostics;
    string? GitDiff;
    List<string> OpenFiles;
    string? ProjectSummary;
}
```

### AgentRouter
```csharp
class AgentRouter {
    IAgentRegistry Registry;
    WorkspaceContextProvider ContextProvider;

    Task RouteAsync(string prompt, RouteOptions options);
    Task RelayAsync(string fromId, AgentResponse resp, string toId);
    WorkspaceContext GatherContext();
}


## Agent Adapter Types

### CLI Adapter (Cline, terminal-based agents)
```
Aero spawns the agent as a child process via pty.
Communication: stdin/stdout, JSON-line protocol.

Lifecycle:
  Start:   `cline --json-mode --workspace /path/to/project`
  Send:    write { "prompt": "...", "context": {...} }\n to stdin
  Receive: read JSON lines from stdout, parse as AgentChunk stream
  Stop:    send { "action": "shutdown" }, wait for process exit
```

### API Adapter (Copilot, GPT-4, Claude, any REST endpoint)
```
HTTP/SSE-based. No process to manage. Just config + auth.

Connection:
  Endpoint: settings.AgentEndpoints["copilot"] = "https://api.github.com/copilot"
  Auth:     OAuth token from settings (stored securely)
  Request:  POST /chat/completions with JSON body
  Stream:   SSE (text/event-stream) or WebSocket for token-by-token
```

### Local Adapter (Ollama, LM Studio, local models)
```
Same as API adapter but pointing to localhost.
  Endpoint: "http://localhost:11434/v1/chat/completions"
  No auth needed (localhost only).
  Model selection: "codellama", "deepseek-coder", etc.
```

---

## Routing Strategies

| Strategy | How It Works |
|----------|-------------|
| **Manual** | User explicitly picks which agent via dropdown |
| **Role-based** | Frontend agent handles conversation/planning; backend agents get code-gen tasks |
| **Capability** | Router matches `CanGenerateCode`/`CanRunCommands` to task type |
| **Pipeline** | Agent A output → Agent B input → Agent C input (chain of agents) |
| **Round-robin** | Prompt goes to all agents; user picks best response |

---

## UI Components for Agents

### AgentPanel (left sidebar)
- Connected agent list with green/red status dots
- Each agent shows role badge (Frontend / Backend)
- Activity spinner when agent is working
- Quick-routing dropdown per agent
- Toggle agents on/off individually

### AgentChat (bottom panel)
- Unified chat input (or per-agent tabs)
- Messages color-coded by agent
- "Thinking..." animation per agent
- Code blocks with "Apply to editor" and "Create file" buttons
- Show agent attribution on every message

---

## Example: "Add login page" (end-to-end)

```
1. User types "Add a login page with email/password" in chat
2. Router gathers context: current project (React+TS), open files, git status
3. Router sends to Cline (frontend): "Plan the login page"
   → Cline: "We need Login.tsx, auth.ts, login.css. I'll spec it."
4. User clicks "Execute"
5. Router sends Cline's spec + context to Copilot (backend):
   "Generate Login.tsx per spec. Use existing Button + Input from src/components/"
6. Copilot streams code token-by-token
7. Router passes Copilot's code to Cline: "Review for consistency"
8. Cline suggests 2 changes → Router sends back to Copilot → regenerates
9. Final code shown with "Apply to editor" button
10. User clicks Apply → opens in new editor tab
```

User speaks once. Agents do the back-and-forth internally.

```

### AgentRegistry
```csharp
interface IAgentRegistry {
    IReadOnlyList<IAgent> Connected { get; }
    void Register(IAgent agent);
    void Unregister(string id);
    IAgent? Get(string id);
}
```

