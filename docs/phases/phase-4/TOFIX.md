# Phase 4 — To Fix

> **Status:** Active — no review findings yet.  
> Resolve all open items before declaring Phase 4 complete.
>
> This file is the persistent code-quality checklist for Phase 4 (Basic LSP
> Integration). Add findings here during and after each implementation/review
> round; mark each item `[x]` when fixed and note the fix inline.

---

## Round 1 — Pre-Implementation Risks (2026-06-19)

These items are known risks before coding starts. They are not all bugs yet,
but each must be verified or resolved during Phase 4.

### R1.1 Language server command resolution is undefined *(priority: high, BLOCKER for M1)*

**Description:** Phase 4 requires spawning `csharp-ls` or `omnisharp`, but the
current codebase has no command-resolution strategy, no settings surface for a
custom server path, and no README instructions for installing either server.
Without a clear resolution policy, startup failures will be opaque and hard to
debug.

**Required fix:** Define one source of truth for server launch configuration in
the first implementation slice. At minimum:

- decide the default binary name(s) to probe
- surface a clear status/error message when the server cannot be started
- document installation/setup in `README.md`

**Status:** [ ] OPEN

### R1.2 `TextDocument` lacks LSP version/URI metadata *(priority: high, BLOCKER for M2)*

**Description:** LSP buffer sync depends on document identity and versioning.
Current `TextDocument` exposes content, file path, display name, and caret state,
but has no document URI helper and no incrementing version number for
`didOpen`/`didChange`.

**Required fix:** Add the minimum metadata required for LSP synchronization and
verify that version updates happen exactly when editor text changes are sent.

**Status:** [ ] OPEN

### R1.3 No bottom-panel host exists for Problems UI *(priority: medium, BLOCKER for M4)*

**Description:** `MainWindow.axaml` currently has sidebar + editor + status bar
only. Phase 4 requires a Problems panel, but there is no existing bottom-panel
layout region. A rushed layout change could bleed into Phase 5/8 concerns.

**Required fix:** Add the smallest possible bottom-panel layout that supports a
read-only Problems view without introducing premature docking/general output
infrastructure.

**Status:** [ ] OPEN

### R1.4 Diagnostic state ownership is not yet defined *(priority: high, BLOCKER for M3)*

**Description:** Diagnostics will arrive asynchronously from the language server.
The codebase currently has no dedicated owner for "latest diagnostics per file"
or for flattening them into a workspace-wide list. If this state is split across
`LSPSession`, `LSPManager`, and UI ViewModels, stale entries and duplicate update
logic are likely.

**Required fix:** Choose a single owner for diagnostic state (recommended:
`LSPManager` or a dedicated diagnostic store) and keep the UI as a consumer only.

**Status:** [ ] OPEN

### R1.5 Completion UI seam is unclear *(priority: medium, BLOCKER for M5)*

**Description:** Phase 4 requires `Ctrl+Space` to trigger LSP completions, but the
current editor integration exposes no completion popup abstraction. A full editor
completion UI may be too large for the first cut.

**Required fix:** Define the minimum acceptable Phase 4 behavior up front.
Examples:

- request completions and log/surface them through a temporary seam, or
- wire a simple popup/list if it can be done without destabilizing the editor

The implementation must not silently claim completion support if only the request
is sent with no observable result.

**Status:** [ ] OPEN

---

## Persistent Checks

Use these as the self-review checklist before closing Phase 4:

- [ ] `StreamJsonRpc` was added only after verifying it matches `docs/LIBRARIES.md`
- [ ] All new services are registered in `src/App.axaml.cs`
- [ ] All new public service methods have tests
- [ ] LSP process startup failures do not crash the app
- [ ] `didOpen` / `didChange` / `didClose` / `didSave` ordering is correct
- [ ] Diagnostics are replaced, not accidentally accumulated forever, per file
- [ ] Closing a document clears its diagnostics from the Problems panel
- [ ] No `async void` was introduced outside Avalonia event handlers
- [ ] No static service access or service locator patterns were introduced
- [ ] `README.md` documents required LSP server installation/setup
- [ ] `dotnet build src/aero.csproj` passes
- [ ] `dotnet test tests` passes
- [ ] Manual Phase 4 smoke test passes
- [ ] `docs/phases/phase-4/TOFIX.md` has no open items before Phase 5 starts