# Phase 4 — Implementation Plan

> **Phase:** 4 — Basic LSP Integration  
> **Date:** 2026-06-19  
> **Status:** Ready to implement

---

## 1. Goal

Add the first usable Language Server Protocol integration to Aero:

- start and initialize a language server process
- keep open editor buffers synchronized with the server
- receive diagnostics and surface them in the editor and a Problems panel
- trigger completion with `Ctrl+Space`

This phase is intentionally **basic**. Hover, go-to-definition, rename, formatting,
semantic tokens, and advanced project-aware server selection remain out of scope.

---

## 2. Entry Check

Phase 4 may begin because the current codebase already has the required Phase 3 baseline:

- `ILanguageDetectionService` is the single source of truth for language identity
- syntax highlighting is wired through editor tabs
- the editor/document stack is stable:
  - `TextDocument`
  - `DocumentManager`
  - `EditorViewModel`
  - `EditorTabViewModel`

Open gap before implementation:

- there is currently **no LSP infrastructure** in `src/`
- there is currently **no Problems panel UI** in `MainWindow.axaml`
- `src/aero.csproj` does **not** yet reference `StreamJsonRpc`

---

## 3. Scope

### In Scope

1. LSP client transport over stdio using JSON-RPC
2. Session/process management for a C# language server
3. Open/change/close/save document notifications
4. Diagnostic storage and propagation
5. Problems panel listing workspace diagnostics
6. `Ctrl+Space` completion request path

### Out of Scope

- hover tooltips
- go to definition / references / rename
- formatting
- signature help
- semantic tokens
- multi-language production support beyond the initial server plumbing
- full solution/project parsing for server startup logic
- auto-installing language servers

---

## 4. Dependency Decision

### Library Choice

Use **`StreamJsonRpc`** for Phase 4.

Reason:

- already documented in `docs/LIBRARIES.md` for Phase 4
- matches the project's library-first rule
- reduces risk in request/response correlation and framing
- keeps the implementation smaller than a custom protocol layer

### Decision Checkpoint

Adding `StreamJsonRpc` is a **new dependency**, but it is already pre-approved by
`docs/LIBRARIES.md` for this phase, so no additional dependency decision is needed.

---

## 5. Architecture Additions

## 5.1 New Services / Components

### `LSPSession`

One running server process and one JSON-RPC connection.

Responsibilities:

- spawn the language server process
- establish JSON-RPC over stdin/stdout
- send initialize / shutdown / exit
- send requests and notifications
- receive server notifications (`publishDiagnostics`, logging)
- dispose process and transport cleanly

Suggested location:

- `src/Languages/LSPSession.cs`

### `LSPManager`

Application-level coordinator for sessions and document synchronization.

Responsibilities:

- create/reuse `LSPSession` instances per language/root
- resolve the language server command
- route document lifecycle events to the correct session
- expose completion requests for the active document
- publish diagnostic updates into the app layer

Suggested location:

- `src/Languages/LSPManager.cs`

### Diagnostic Store / Problems ViewModel

Responsibilities:

- maintain the latest diagnostics per file URI
- flatten diagnostics into a workspace-wide list for the Problems panel
- expose severity, file, line, column, and message

Suggested locations:

- `src/Models/Languages/` for DTOs
- `src/ViewModels/ProblemsViewModel.cs`
- `src/Views/ProblemsView.axaml`

---

## 5.2 Model Changes

### `TextDocument`

Add the minimum metadata required for LSP synchronization:

- stable document URI derived from `FilePath`
- integer version number
- helpers for incrementing version on text change

Notes:

- untitled documents do not need full LSP support in the first cut; they may remain
  local-only until saved if that simplifies the first implementation
- avoid making the model aware of transport details; keep it to metadata only

### Diagnostic Models

Add small internal models that match the needed LSP payloads:

- diagnostic severity
- file URI/path
- range (start/end line/column)
- message
- source/code if available

Keep them focused and UI-friendly rather than mirroring the full LSP spec everywhere.

---

## 5.3 UI Changes

### `MainWindow.axaml`

Current layout is:

- sidebar
- editor
- status bar

Phase 4 adds a bottom Problems area.

Minimum layout change:

- split the main editor column into:
  - editor area
  - problems panel area
- allow the problems panel to be collapsed when empty or hidden

### Problems Panel

Requirements:

- list all current diagnostics in the workspace
- show severity
- show file name
- show line/column
- show message

Optional but useful in the first cut:

- selecting an item activates the file and moves the caret if the existing editor API
  can support that without large extra work

---

## 5.4 Message Bus Integration

Use the existing `MessageBus` for app-level communication.

Add messages for:

- diagnostics updated for a document/workspace
- optional request to jump to a diagnostic location
- optional LSP/log status message for status bar visibility

Do **not** bypass the existing app messaging style with static state.

---

## 6. Milestones

## M1 — LSP Plumbing

Deliverables:

- add `StreamJsonRpc` to `src/aero.csproj`
- implement `LSPSession`
- implement server process startup/shutdown
- send `initialize` and `initialized`
- add tests around session setup logic where practical

Gate:

- application builds
- a session can start for a configured C# server command

## M2 — Document Synchronization

Deliverables:

- `didOpen`
- debounced `didChange`
- `didClose`
- `didSave`
- version tracking in `TextDocument`

Gate:

- buffer updates flow to the server in the correct order

## M3 — Diagnostics

Deliverables:

- receive `textDocument/publishDiagnostics`
- convert diagnostics to internal models
- maintain latest diagnostics by file
- expose workspace diagnostics to the UI

Gate:

- a C# syntax error appears in the diagnostic store and is visible in the UI

## M4 — Problems Panel

Deliverables:

- add `ProblemsViewModel`
- add `ProblemsView.axaml`
- integrate panel into `MainWindow.axaml`

Gate:

- panel lists all current diagnostics and updates when errors are fixed

## M5 — Completion Trigger

Deliverables:

- add `Ctrl+Space` keybinding/command
- request `textDocument/completion` for active document/caret
- return and surface completion results through the existing editor integration seam

Gate:

- `Ctrl+Space` reaches the server successfully without breaking editor input

---

## 7. File Plan

### New files (expected)

- `src/Languages/LSPSession.cs`
- `src/Languages/LSPManager.cs`
- `src/Languages/LspProtocolModels.cs` *(or a small set of focused model files)*
- `src/ViewModels/ProblemsViewModel.cs`
- `src/Views/ProblemsView.axaml`
- `src/Views/ProblemsView.axaml.cs` *(only if needed)*
- `tests/Languages/LSPSessionTests.cs`
- `tests/Languages/LSPManagerTests.cs`
- `docs/phases/phase-4/TOFIX.md`

### Existing files likely to change

- `src/aero.csproj`
- `src/App.axaml.cs`
- `src/Models/Editor/TextDocument.cs`
- `src/Services/DocumentManager.cs`
- `src/ViewModels/EditorViewModel.cs`
- `src/ViewModels/ShellViewModel.cs`
- `src/MainWindow.axaml`
- `src/Core/Messages.cs`
- `README.md`
- `docs/roadmap/PHASES.md`

---

## 8. Testing Plan

### Unit Tests

- URI/version behavior on `TextDocument`
- session command resolution / startup validation
- diagnostic aggregation logic
- problems list flattening / ordering

### Integration-Style Tests

- `LSPManager` routes open/change/close to the session abstraction
- diagnostics message updates ViewModel state correctly

### Manual Smoke

Target scenario for Phase 4 completion:

1. open a C# folder/project
2. open a `.cs` file
3. introduce a syntax error
4. observe diagnostic in Problems panel
5. fix the error
6. observe diagnostic removal
7. press `Ctrl+Space`
8. confirm completion request path is alive

---

## 9. Risks

### Highest Risk — Server availability

`csharp-ls` or `omnisharp` are external binaries.

Mitigation:

- support a clearly documented command path/lookup strategy
- fail gracefully with a visible status message
- document installation in `README.md`

### High Risk — Incorrect buffer sync

If `didOpen`/`didChange`/`didClose` ordering or versioning is wrong, diagnostics and
completion will be misleading.

Mitigation:

- keep sync logic centralized in `LSPManager`
- debounce only where intended
- add tests around event ordering

### Medium Risk — UI scope growth

Problems panel work can sprawl into Phase 5/6 territory.

Mitigation:

- keep Phase 4 Problems panel read-only and minimal
- do not build a generalized output/log docking system yet

---

## 10. Definition of Done

Phase 4 is done when all of the following are true:

- a language server session starts successfully for C#
- open editor buffers are synchronized to the server
- diagnostics are received and stored correctly
- a Problems panel shows current workspace diagnostics
- `Ctrl+Space` triggers a completion request path
- build passes: `dotnet build src/aero.csproj`
- tests pass: `dotnet test tests`
- Phase 4 checklist in `docs/roadmap/PHASES.md` is updated
- README documents required server installation/setup

---

## 11. Notes for Implementation

- Prefer one focused change per milestone.
- Keep DTOs small; do not import the entire LSP surface unless needed.
- Use constructor injection and register all new services in `src/App.axaml.cs`.
- If the first server integration becomes unclear after two implementation attempts,
  create an issue in `docs/issues/open/` immediately per `AGENTS.md`.