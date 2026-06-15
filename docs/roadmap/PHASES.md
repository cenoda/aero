# Aero IDE — Development Roadmap

Two tracks: **IDE Core** (the code editor) and **Agent Layer** (the AI orchestration).
Build the IDE first so it's usable standalone, then add agents to supercharge it.

## Phase 0: Foundation
- [x] Avalonia project scaffold
- [x] Basic window with title
- [ ] Create directory skeleton (Models/, Services/, ViewModels/, Views/, Agent/, etc.)
- [ ] Core infrastructure: `ObservableObject`, `RelayCommand`, `MessageBus`
- [ ] Add AvaloniaEdit NuGet package
- [ ] Basic DI with Microsoft.Extensions.DependencyInjection

## Phase 1: The Editor
- [ ] **TextBuffer** — efficient gap-buffer or rope structure
- [ ] **TextEditor view** — AvaloniaEdit integration with line numbers
- [ ] **Document model** — open/close files, dirty flag
- [ ] **Tabbed editor** — multiple open files with tabs
- [ ] **File open/save** — Ctrl+O, Ctrl+S, file dialogs
- [ ] **Undo/Redo** — command-pattern undo stack (Ctrl+Z / Ctrl+Y)
- [ ] **Find/Replace** — Ctrl+F with overlay panel
- [ ] Status bar shows cursor position (Ln X, Col Y)

## Phase 2: File Explorer & Project System
- [ ] **FileExplorer panel** — tree view of a folder
- [ ] **Open Folder** — File → Open Folder, populates the tree
- [ ] **FileSystemWatcher** — auto-refresh on external changes
- [ ] **ProjectLoader** — recognize .sln, .csproj, package.json
- [ ] Context menu: New File, New Folder, Delete, Rename
- [ ] Click file in tree → opens in editor

## Phase 3: Syntax Highlighting
- [ ] **LanguageDefinition** registry (C#, JSON, XML, Markdown, etc.)
- [ ] **TextMate grammar loader** — load .tmLanguage JSON
- [ ] Wire grammar to AvaloniaEdit highlighting
- [ ] Auto-detect language from file extension
- [ ] Status bar shows current language

## Phase 4: Basic LSP Integration
- [ ] **LSPSession** — JSON-RPC over stdin/stdout to a language server
- [ ] **LSPManager** — spawn `csharp-ls` / `omnisharp` per project
- [ ] **Diagnostics** — red squigglies on errors
- [ ] **Problems panel** — list all diagnostics in workspace
- [ ] **Code completion** — Ctrl+Space triggers LSP completions

## Phase 5: Terminal
- [ ] **PtyProcess** — spawn shell with pseudo-terminal
- [ ] **TerminalEmulator** — parse escape sequences
- [ ] **TerminalRenderer** — draw to Avalonia canvas
- [ ] Terminal panel in bottom dock
- [ ] Multiple terminal tabs
- [ ] Ctrl+` to toggle terminal

## Phase 6: Build & Output
- [ ] **BuildService** — run `dotnet build` and capture output
- [ ] **Output panel** — stream stdout/stderr
- [ ] Parse MSBuild error format → populate Problems panel
- [ ] Ctrl+Shift+B to build
- [ ] Click error in Problems → jump to file/line

## Phase 7: Git Integration
- [ ] **GitRepository** — wrap git CLI or libgit2sharp
- [ ] **Git panel** — staged/unstaged changes list
- [ ] **Diff viewer** — inline diff with +/- gutter
- [ ] Commit UI (message, stage/unstage, commit button)
- [ ] Branch indicator in status bar
- [ ] File modified indicator in editor tab and file tree

## Phase 8: UI Polish
- [ ] **Dockable panels** — drag to rearrange layout
- [ ] **Theme system** — light/dark switch
- [ ] **Command palette** — Ctrl+Shift+P fuzzy search
- [ ] **Keybinding config** — customizable shortcuts
- [ ] **Welcome page** — recent projects, new file, etc.
- [ ] **Settings page** — preferences UI (font, theme, tab size, etc.)

## Phase 9: Advanced Features
- [ ] **Multi-cursor editing** — Ctrl+D to select next occurrence
- [ ] **Minimap** — scrollable code overview
- [ ] **Snippets** — configurable code snippets
- [ ] **Go to definition** — via LSP
- [ ] **Rename symbol** — via LSP
- [ ] **Format document** — via LSP

## Phase 10: Plugin System
- [ ] **IPlugin interface** — Initialize(), Shutdown(), metadata
- [ ] **PluginHost** — scan & load assemblies
- [ ] **Extension points** — register commands, languages, themes
- [ ] **Plugin marketplace** — optional: discover & install


---

## AGENT TRACK: Multi-Agent AI Orchestration

### Phase A1: Agent Foundation
- [ ] **IAgent interface** — Id, Name, Kind (CLI/API/Local), Role (Frontend/Backend)
- [ ] **AgentRegistry** — discover/register/unregister agents
- [ ] **WorkspaceContext** — gather open files, cursor, diagnostics, git diff
- [ ] **AgentPanel UI** — sidebar listing connected agents with status dots
- [ ] **AgentChat UI** — bottom panel with chat input, message list, agent colors
- [ ] Agent configuration in settings (endpoint URLs, API keys, CLI paths)

### Phase A2: Single Agent (Cline via CLI)
- [ ] **CliAgentAdapter** — spawn Cline as child process over pty
- [ ] JSON-line protocol: send prompt, receive streamed response
- [ ] AgentChat hooked to Cline — type prompt, see response stream in
- [ ] "Apply to editor" button on code blocks
- [ ] Context auto-injection (active file + cursor before every prompt)

### Phase A3: Multi-Agent Routing
- [ ] **AgentRouter** — route prompts by role, capability, or user choice
- [ ] Agent dropdown in chat to pick target
- [ ] **ApiAgentAdapter** — connect to Copilot/GPT via HTTP/SSE
- [ ] **LocalAgentAdapter** — connect to Ollama/LM Studio on localhost
- [ ] Multiple agents running simultaneously in chat (parallel tabs or threads)

### Phase A4: Agent-to-Agent Pipeline
- [ ] **Relay** — Agent A output auto-routed to Agent B
- [ ] Pipeline configuration: "Cline plans → Copilot codes → Cline reviews"
- [ ] **Frontend/Backend split** — Cline talks to user, Copilot does silent work
- [ ] Agent activity indicators in AgentPanel (which agent is working right now)
- [ ] Inter-agent message log viewer

### Phase A5: Advanced Agent Features
- [ ] **Agent memory** — persistent conversation context per agent
- [ ] **Tool use** — agents can trigger IDE actions (open file, run build, git commit)
- [ ] **Agent marketplace** — discover and install community agent adapters
- [ ] **Custom routing rules** — user-defined "if task=X, route to agent Y"
- [ ] **Agent comparison mode** — send same prompt to all, compare outputs side-by-side
