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
- [x] `IFileSystemWatcherService` wrapper over `FileSystemWatcher`
- [x] Debounce/batch rapid events (300 ms default window)
- [x] Publish `FolderChanged` for full subtree refresh; per-node refresh is deferred (reloading the root is fast enough for Phase 2)
- [x] Graceful error handling: permission denied, deleted folder, inotify limits → status bar / log message; manual refresh still works

### 2.4 Project Awareness
- [x] `IProjectLoader` service: extension-based recognition of `.sln`, `.csproj`, `package.json`. Full MSBuild/SLN parsing is deferred to Phase 6.
- [x] `ProjectInfo` model: name, type (Solution / C# Project / Node Package), path
- [x] Highlight solution/project roots in the tree with project-specific icons. **Deferred to Phase 8** — blocked on the icon-library decision (TOFIX R3.1: `Material.Icons.Avalonia` is broken on Avalonia 11.3; the tree uses text glyphs and the VM records `IconKind` strings for forward-compat). Full project-node sub-trees also deferred.
- [x] Keep the loader read-only; do not modify project files

### 2.5 Workspace Persistence (stub)
- [x] Deferred to Phase 8 (Settings). Phase 2 does not persist the last-opened folder or tree expansion across sessions. The Phase 8 settings system will absorb this naturally.

### 2.6 Tests
- [x] Unit tests for `IIgnoreList` pattern matching
- [x] Unit tests for `FileExplorerViewModel` tree-building and command behavior (via in-memory stubs)
- [x] Integration tests for `FileSystemService` (temp-dir I/O), `ProjectLoader` (recognition)
- [x] Integration tests for `FileSystemWatcherService` (debounce)
- [x] Phase 1 regression: all 89 existing tests continue to pass (227/227 total as of M5)


## Phase 3: Syntax Highlighting

> Goal: Auto-detect language from extension and apply TextMate syntax highlighting.
> Entry condition: Phase 2 is complete.
> Implementation details: [`docs/phases/phase-3/PROJECT_PLAN.md`](../phases/phase-3/PROJECT_PLAN.md).

- [x] **Language detection service** — extension → TextMate id + display name; single source of truth via `ILanguageDetectionService`
- [x] **TextMate grammar loader** — bundled grammars via `TextMateSharp.Grammars` `RegistryOptions`
- [x] Wire grammar to AvaloniaEdit highlighting
- [x] Auto-detect language from file extension
- [x] Status bar shows current language

## Phase 4: Basic LSP Integration
> Goal: Connect to language servers for diagnostics, completion, and the Problems panel.
> Entry condition: Phase 3 is complete.
> Implementation details: [`docs/phases/phase-4/IMPLEMENTATION_PLAN.md`](../phases/phase-4/IMPLEMENTATION_PLAN.md).

- [x] **LSPSession** — JSON-RPC over stdin/stdout to a language server (M1)
- [x] **LSPManager** — document synchronization: didOpen/didChange/didSave/didClose (M2)
- [x] **Diagnostics** — red squigglies on errors (M3)
- [x] **Problems panel** — list all diagnostics in workspace (M4)
- [x] **Code completion** — Ctrl+Space triggers LSP completions (M5)

## Phase 5: Output Panel (가짜 터미널) ✅
> ⚠️ 진짜 인터랙티브 터미널(PTY)은 Phase 9.5로 이동. OS마다 PTY 구현이 달라 난이도가 매우 높음.
> 지금은 명령 실행 결과를 텍스트로 보여주는 Output Panel로 대체한다.
> Implementation details: [`docs/phases/phase-5/IMPLEMENTATION_PLAN.md`](../phases/phase-5/IMPLEMENTATION_PLAN.md).

- [x] **ProcessRunner** — `CliWrap`으로 커맨드 실행 (dotnet, git 등)
- [x] **Output panel** — stdout/stderr 실시간 스트리밍
- [x] Ctrl+` 로 패널 토글
- [x] 실행 중 취소 버튼 (CancellationToken)
- [x] Bottom panel refactored to host Problems + Output as sibling tabs

## Phase 6: Multi-language Build System ✅
> Goal: Integrate build systems with the IDE using **abstraction-first** design for multi-language support.
> Entry condition: Phase 5 complete (Output panel, ProcessRunner)
> Implementation details: [`docs/phases/phase-6/IMPLEMENTATION_PLAN.md`](../phases/phase-6/IMPLEMENTATION_PLAN.md).

- [x] **IBuildService** — abstraction over a build system (interface-first, per `AGENTS.md` §4)
- [x] **DotNetBuildService** — the **only** concrete implementation this phase (`.sln` / `.csproj`)
- [x] **BuildServiceFactory** — auto-detects the workspace build system (reuses `IProjectLoader`)
- [x] **BuildOptions** / **BuildResult** / **ParsedError** models
- [x] **Ctrl+Shift+B** runs the detected build; output streams into the existing Output tab
- [x] Build errors/warnings populate the existing **Problems** panel (alongside LSP diagnostics)
- [x] Clicking a problem opens the file and moves the caret to the line/column
- [x] Build state is surfaced in the status bar (`Building… / Build succeeded / Build failed`)
- [x] Build diagnostics coexist with LSP diagnostics for the same file (source-based keying)
- [x] Single active build enforced with proper cancellation
- [x] Graceful error handling (dotnet not found, etc.)
- [x] All tests passing (328/328)
- [x] Manual test passing
- [x] **Review:** See `PHASE6_CONSOLIDATED_REVIEW.md` for comprehensive review

## Phase 5.5: Abstraction Implementation Pass
> Review completed phases (0-5) and implement abstraction-first design.

- [x] **Phase 0** — Verify DI and service registration
- [x] **Phase 1** — Review DocumentManager
- [x] **Phase 2** — Review IFileSystemService, IProjectLoader
- [x] **Phase 3** — Add ISyntaxHighlighterService
- [x] **Phase 4** — Verify ILSPService
- [x] **Phase 5** — Add IProcessRunnerService

## Phase 6: Build & Output (Abstraction-First)
> Multi-language build system support. Interface-first design for extensibility.

- [x] **IBuildService interface** — abstraction with BuildCommand, ProjectFilePattern
- [x] **BuildOptions / BuildResult models** — configuration and output
- [x] **DotNetBuildService** — implements IBuildService for .NET
- [x] **BuildServiceFactory** — auto-detect project type and create service
- [x] **Output panel** — stream stdout/stderr (reuses Phase 5)
- [x] Parse build error format → populate Problems panel
- [x] Ctrl+Shift+B to build
- [x] Click error in Problems → jump to file/line

## Phase 7: Git Integration
- [x] **GitRepository** — wrap git CLI or libgit2sharp
- [x] **Git panel** — staged/unstaged changes list
- [x] **Diff viewer** — inline diff with +/- gutter
- [x] Commit UI (message, stage/unstage, commit button)
- [x] Branch indicator in status bar
- [x] File modified indicator in editor tab and file tree

### Phase 7 Extensions (post-baseline additions — see `docs/phases/phase-7/EXTENSIONS.md`)
- [x] **Branch graph** — visual DAG of commit history with lane coloring, branch labels, and commit detail panel
- [x] **Auto-reload** — `GitWatcher` on `.git/HEAD` + `.git/index`; panel refreshes automatically on external git operations

## Phase 8: Core UI Polish — Design & User Choice

> **This is the first phase where your intent is the product.**
> Previous phases (0–7) were engineering — LSP, Git, Build — where there's a right answer.
> Phase 8 is design — there's no right answer, only *your taste*.
>
> ### Design Target: JetBrains Rider level
>
> > "Honestly, the minimum MVP is JetBrains Rider quality. Only then does this mean something."
>
| VS Code (what we're *not*) | Aero (what we're *going for*) |
|-----------------------------|-------------------------------|
| Flat, angular, clunky | Rounded, clean, smooth |
| Forced single layout | User-configurable orchestration environment |
| 15 theme colors | **115 color tokens, fully overridable** via JSON |
| No design system | Unified spacing/radius/shadow/transition (Apple/Rider inspired) |
>
> ### Philosophy: User Choice, Not Lock-In
>
> > "Aero is not just an IDE — it's a platform where users configure their own orchestration environment."
>
> | User Type | Need | Aero's Response |
> |-----------|------|-----------------|
> | **Conservative** | Traditional IDE (text center, agent right) | Preset layout provided |
> | **Progressive** | VS Code style (agent as main view) | Preset layout provided |
> | **Ultra-progressive** | Multi-agent view (split, pipeline, multiple windows) | Dock.Avalonia full custom |
> | **Orchestration** | Auto-route Agent A → Agent B | Phase A4 (Agent-to-Agent Pipeline) |
>
> Every Phase 8 decision answers: *"Does this give the user choice, or does it force them?"*
>
> ---
>
> **Entry condition:** Phase 7 is complete.
> **Exit condition:** Panels are dockable in two modes; theme has 115 tokens with JSON override; command palette, welcome page, icons, settings, persistence, keybinding reference, and design system all deliverable.

### 8.1 Dockable Panels — Layout Modes [`docs/phases/phase-8/8.1-dockable-panels/`](docs/phases/phase-8/8.1-dockable-panels/)
- [ ] **8.1a Freeform Mode** — wire existing panels into Dock.Avalonia (draggable, resizable, hideable, rearrangeable)
- [ ] **8.1b Tile Mode** — auto-layout with tiling + stack (tab) support. Panels tile side-by-side, stack when overlapped. Keyboard-navigation optimized. Manual adjustment still allowed (avoids Hyperland limitation).
- [ ] **8.1c Tear-Away Windows** — panels can be dragged out of the main window into standalone OS windows (Chrome tab style). Re-dockable.
- [ ] **Tile + Stack mixing** — panels tile when placed side-by-side, merge into tabs when placed in the same slot.
- [ ] Mode switching in settings; no restart required.
- [ ] Principles: Tile Mode *must* still allow manual adjustment. Mode switch must be instant.

### 8.2 Theme Engine — 115 Color Tokens + JSON Override [`docs/phases/phase-8/8.2-theme-system/`](docs/phases/phase-8/8.2-theme-system/)
- [ ] Define **115 semantic color tokens** (editor bg/fg/selection, scrollbar, button hover, tab active underline, inline hints, debug states, panel headers, borders, git states, etc.) — full inventory in [`TOKENS.md`](docs/phases/phase-8/8.2-theme-system/TOKENS.md)
- [ ] Create Light and Dark presets stored as ResourceDictionary in `src/Styles/`
- [ ] **User JSON override** — `~/.aero/theme-override.json` lets the user customize every single color token (VS Code `workbench.colorCustomizations` style)
- [ ] Switch via `App.Current.RequestedThemeVariant`; status bar shows current theme
- [ ] Ctrl+Shift+T or View → Toggle Theme

### 8.3 Command Palette [`docs/phases/phase-8/8.3-command-palette/`](docs/phases/phase-8/8.3-command-palette/)
- [ ] Define `ICommandRegistry` with command metadata (shared with 8.8)
- [ ] Ctrl+Shift+P opens DialogHost overlay with FuzzySharp-filtered command list
- [ ] Enter executes, Esc dismisses; keyboard hints shown

### 8.4 Welcome Page [`docs/phases/phase-8/8.4-welcome-page/`](docs/phases/phase-8/8.4-welcome-page/)
- [ ] Landing tab when no files open: recent folders (from 8.7), quick-action buttons
- [ ] Empty state on first launch (no recent folders)

### 8.5 Icon Decision & Integration [`docs/phases/phase-8/8.5-icon-decision/`](docs/phases/phase-8/8.5-icon-decision/)
- [x] Resolve TOFIX R3.1 — **commit to text glyphs for Phase 8** (no new icon package)
- [ ] Apply consistent file-type glyphs to tree (8 types: folder, code, text, image, config, markup, project, unknown)
- [ ] Apply tab glyphs in editor tabs
- [ ] Update `docs/LIBRARIES.md` with the decision

### 8.6 Settings Page [`docs/phases/phase-8/8.6-settings-page/`](docs/phases/phase-8/8.6-settings-page/)
- [ ] Single dialog: Font, Theme, Tab Size, Editor options, **Layout Mode selector** (Tile/Freeform)
- [ ] Persist to `~/.aero/settings.json` (via ISettingsService from 8.7); immediate apply, no restart

### 8.7 Workspace Persistence [`docs/phases/phase-8/8.7-workspace-persistence/`](docs/phases/phase-8/8.7-workspace-persistence/)
- [ ] Define `ISettingsService` / `SettingsService` (shared persistence layer for 8.4, 8.6)
- [ ] Remember last folder, open files, active tab, window position across restarts (`~/.aero/workspace.json`)
- [ ] Remember recent folders list (for 8.4 Welcome Page)

### 8.8 Keybinding Display (Read-Only) [`docs/phases/phase-8/8.8-keybinding-display/`](docs/phases/phase-8/8.8-keybinding-display/)
- [ ] Define `ICommandRegistry` (shared with 8.3) if not already done
- [ ] Categorized read-only keyboard shortcuts reference
- [ ] Custom keybinding editing deferred to **Phase 9: Advanced Features**

### 8.9 Design System Foundation [`docs/phases/phase-8/8.9-design-system/`](docs/phases/phase-8/8.9-design-system/)
> ⚠️ **Execute this sub-phase FIRST.** All other sub-phases depend on it.
- [ ] **Spacing Scale** — 4px grid base (4/8/12/16/24/32). Applied to button padding, list item height, section gaps, panel margins.
- [ ] **Corner Radius** — unified rounding for buttons (`6px`), inputs (`6px`), panels (`8px`), popups (`10px`), tabs (`4px` top only), scrollbar thumbs (`4px`)
- [ ] **Shadow System** — layered depth (subtle `0 1px 2px rgba(0,0,0,0.08)` / medium `0 4px 12px rgba(0,0,0,0.12)` / popup `0 8px 24px rgba(0,0,0,0.16)`)
- [ ] **Transition Timing** — 200ms `CubicOut` as default for hover, focus, panel open/close, tab switch, color change
- [ ] **Typography** — Inter font, size scale: 11px (status bar), 12px (UI labels), 13px (body), 14px (tab titles), 16px (heading)
- [ ] **Border System** — unified 1px Solid border for panel dividers, inputs, grid lines
- [ ] **Color Token Naming Convention** — `{area}.{property}` e.g. `editor.background`, `panel.border`, `button.hoverBackground`. Feeds into 8.2 Theme Engine.
- [ ] Output as Avalonia ResourceDictionary files in `src/Styles/` (`Spacing.axaml`, `CornerRadius.axaml`, `Shadows.axaml`, `Transitions.axaml`, `Typography.axaml`, `Borders.axaml`)
- [ ] Exact values set by design agent; this folder defines scope only

### Phase 8 Tests (All Sub-Phases)
- [ ] Unit tests for command registry, theme tokens, settings I/O, glyph mapping
- [ ] Integration tests for settings round-trip, workspace persistence, theme switch
- [ ] Manual tests for dockable panels (drag, resize, tear-away, mode switch)
- [ ] All pre-existing tests continue to pass

## Phase 9: Advanced Features

> Goal: Power-user editing features and LSP-driven code intelligence.
> Entry condition: Phase 8 is complete.

- [ ] **Keybinding editing** — customizable shortcuts with conflict detection (moved from Phase 8)
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
> ⚠️ Phase 8 (Core UI Polish — especially dockable panels) 완료 후 시작할 것. 그 전에 시작하면 레이아웃 완성 후 전부 재작업해야 함.

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
