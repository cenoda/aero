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
- [x] **TextBuffer** — AvaloniaEdit's built-in piece-table (TextDocument)
- [x] **TextEditor view** — AvaloniaEdit integration with line numbers
- [x] **Document model** — open/close files, dirty flag
- [x] **Tabbed editor** — multiple open files with tabs
- [x] **File open/save** — Ctrl+O, Ctrl+S, file dialogs
- [x] **Undo/Redo** — AvaloniaEdit undo stack (Ctrl+Z / Ctrl+Y)
- [x] **Find/Replace** — Ctrl+F with overlay panel
- [x] Status bar shows cursor position (Ln X, Col Y)

## Phase 2: File Explorer & Project System

> Goal: Add a performant, project-aware **File Explorer** sidebar that is ready for syntax highlighting, LSP, and build integration.
> Entry condition: Phase 1 is complete.
> Implementation details: [`docs/phases/phase-2/PROJECT_PLAN.md`](../phases/phase-2/PROJECT_PLAN.md).

### 2.1 Tree UI & File Operations
- [x] `FileExplorerView` sidebar with `TreeView`, keyboard navigation, and Material icons
- [x] `FileSystemEntry` model: file vs directory, name, full path
- [x] Eager tree load (full enumeration off UI thread). Lazy load on expand is deferred — `IIgnoreList` prevents `node_modules`/`bin`/`obj` freezes in the common case; large-monorepo optimization is a follow-up.
- [x] `File → Open Folder` command via Avalonia folder picker (`Ctrl+Shift+O`)
- [x] Click file in tree → open in editor via `DocumentManager.OpenDocumentAsync`
- [x] Context menu: **New File**, **New Folder**, **Delete**, **Rename** with name validation

### 2.2 Filtering & Large-Directory Safety
- [x] Default ignore list: `node_modules`, `bin`, `obj`, `.git`, `.vs`, `packages`
- [x] `IIgnoreList` / `IgnoreList` service with unit-testable pattern matching (custom code, no new NuGet)
- [x] Hide ignored folders from tree enumeration and `FileSystemWatcher` notifications
- [x] Async enumeration with `CancellationToken` so the UI never blocks

### 2.3 Live Sync
- [ ] `IFileSystemWatcherService` wrapper over `FileSystemWatcher`
- [ ] Debounce/batch rapid events (300 ms default window)
- [ ] Publish `FolderChanged` for full subtree refresh; per-node refresh is deferred (reloading the root is fast enough for Phase 2)
- [ ] Graceful error handling: permission denied, deleted folder, inotify limits → status bar / log message; manual refresh still works

### 2.4 Project Awareness
- [x] `IProjectLoader` service: extension-based recognition of `.sln`, `.csproj`, `package.json`. Full MSBuild/SLN parsing is deferred to Phase 6.
- [x] `ProjectInfo` model: name, type (Solution / C# Project / Node Package), path
- [ ] Highlight solution/project roots in the tree with project-specific icons. Full project-node sub-trees (listing project children inline) is deferred.
- [x] Keep the loader read-only; do not modify project files

### 2.5 Workspace Persistence (stub)
- [ ] Deferred to Phase 8 (Settings). Phase 2 does not persist the last-opened folder or tree expansion across sessions. The Phase 8 settings system will absorb this naturally.

### 2.6 Tests
- [x] Unit tests for `IIgnoreList` pattern matching
- [x] Unit tests for `FileExplorerViewModel` tree-building and command behavior (via in-memory stubs)
- [x] Integration tests for `FileSystemService` (temp-dir I/O), `ProjectLoader` (recognition)
- [ ] Integration tests for `FileSystemWatcherService` (debounce) — pending M5
- [x] Phase 1 regression: all 89 existing tests continue to pass (219/219 total as of M4)


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
