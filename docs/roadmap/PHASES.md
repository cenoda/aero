# Aero IDE — Development Roadmap

Two tracks: **IDE Core** (the code editor) and **Agent Layer** (the AI orchestration).
Build the IDE first so it's usable standalone, then add agents to supercharge it.

## Phase 0: Foundation
- [x] Avalonia project scaffold
- [x] Basic window with title
- [x] Create directory skeleton (Models/, Services/, ViewModels/, Views/, Agent/, etc.)
- [x] Core infrastructure: ReactiveUI + Microsoft.Extensions.DependencyInjection 세팅
- [x] Add AvaloniaEdit NuGet package
- [x] DI 컨테이너 구성 (Program.cs에 서비스 등록)

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

## Phase 5: Output Panel (가짜 터미널)
> ⚠️ 진짜 인터랙티브 터미널(PTY)은 Phase 9로 이동. OS마다 PTY 구현이 달라 난이도가 매우 높음.
> 지금은 명령 실행 결과를 텍스트로 보여주는 Output Panel로 대체한다.

- [ ] **ProcessRunner** — `CliWrap`으로 커맨드 실행 (dotnet, git 등)
- [ ] **Output panel** — stdout/stderr 실시간 스트리밍
- [ ] Ctrl+` 로 패널 토글
- [ ] 실행 중 취소 버튼 (CancellationToken)

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

### Phase 9.5: 진짜 터미널 (선택 사항)
> ⚠️ 난이도 높음. IDE 나머지가 다 완성된 후 도전할 것.

- [ ] **Pty.Net** — OS별 PTY 연결 (Linux/Mac/Windows)
- [ ] **VtNetCore** — VT100/xterm 이스케이프 코드 파싱
- [ ] **TerminalRenderer** — Avalonia 캔버스에 직접 렌더링
- [ ] 인터랙티브 쉘 (bash / cmd / powershell)
- [ ] 여러 터미널 탭

## Phase 10: Plugin System
- [ ] **IPlugin interface** — Initialize(), Shutdown(), metadata
- [ ] **PluginHost** — scan & load assemblies
- [ ] **Extension points** — register commands, languages, themes
- [ ] **Plugin marketplace** — optional: discover & install


---

## AGENT TRACK: Multi-Agent AI Orchestration

### Phase A1: Agent Foundation
> ⚠️ Phase 8 (UI Polish / Docking) 완료 후 시작할 것. 그 전에 시작하면 레이아웃 완성 후 전부 재작업해야 함.

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
