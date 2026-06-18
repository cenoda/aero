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

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — Phase 4 now commits to `csharp-ls`
as the primary C# server, with graceful failure and README installation docs required.

### R1.2 `TextDocument` lacks LSP version/URI metadata *(priority: high, BLOCKER for M2)*

**Description:** LSP buffer sync depends on document identity and versioning.
Current `TextDocument` exposes content, file path, display name, and caret state,
but has no document URI helper and no incrementing version number for
`didOpen`/`didChange`.

**Required fix:** Add the minimum metadata required for LSP synchronization and
verify that version updates happen exactly when editor text changes are sent.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now explicitly
requires URI/version metadata on `TextDocument`. Implementation remains pending and must
be verified during M2.

### R1.3 `didChange` sync mode is unspecified *(priority: high, BLOCKER for M2)*

**Description:** The original plan did not choose between incremental and full-document
sync for `textDocument/didChange`. For a first LSP phase, leaving this undecided raises
risk in testing, versioning, and document update correctness.

**Required fix:** Lock Phase 4 to **full-document sync** and test only that path.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — the implementation plan now explicitly
chooses full-document `didChange` sync.

### R1.4 No bottom-panel host exists for Problems UI *(priority: medium, BLOCKER for M4)*

**Description:** `MainWindow.axaml` currently has sidebar + editor + status bar
only. Phase 4 requires a Problems panel, but there is no existing bottom-panel
layout region. A rushed layout change could bleed into Phase 5/8 concerns.

**Required fix:** Add the smallest possible bottom-panel layout that supports a
read-only Problems view without introducing premature docking/general output
infrastructure.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now explicitly
requires the smallest possible bottom-panel layout. Implementation remains pending and
must be verified during M4.

### R1.5 Diagnostic state ownership is not yet defined *(priority: high, BLOCKER for M3)*

**Description:** Diagnostics will arrive asynchronously from the language server.
The codebase currently has no dedicated owner for "latest diagnostics per file"
or for flattening them into a workspace-wide list. If this state is split across
`LSPSession`, `LSPManager`, and UI ViewModels, stale entries and duplicate update
logic are likely.

**Required fix:** Keep `LSPManager` as the single owner for diagnostic state and keep
the UI as a consumer only.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — `LSPManager` is now the single
source of truth for diagnostics in the implementation plan. Implementation remains pending
and must be verified during M3.

### R1.6 Completion UI seam is unclear *(priority: medium, BLOCKER for M5)*

**Description:** Phase 4 requires `Ctrl+Space` to trigger LSP completions, but the
current editor integration exposes no completion popup abstraction. A full editor
completion UI may be too large for the first cut.

**Required fix:** Define the minimum acceptable Phase 4 behavior up front.
The implementation must not silently claim completion support if only the request
is sent with no observable result.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — Phase 4 now requires a visible
completion popup/list or equivalent observable completion UI.

### R1.7 Diagnostics rendering seam is unclear *(priority: medium, BLOCKER for M3)*

**Description:** The roadmap requires red squigglies/errors in the editor, but the
original plan did not specify how AvaloniaEdit would render diagnostics.

**Required fix:** Define and implement an AvaloniaEdit marker service seam for active-file
diagnostic rendering, or explicitly document a constrained fallback if full squiggles
prove unstable.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — the implementation plan now requires
an AvaloniaEdit marker-service integration for editor-visible diagnostics.

### R1.8 Session scope/root selection was vague *(priority: medium, BLOCKER for M1)*

**Description:** The original wording "per language/root" was too vague without Phase 6
project parsing. Session scope must be simple and explicit for the first cut.

**Required fix:** Lock Phase 4 to one C# LSP session per opened folder, using the
opened folder as `rootUri`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — the implementation plan now defines one
session per opened folder.

---

## Round 2 — Integration Review (2026-06-19)

Findings from reviewing the implementation plan against the live codebase and prior phases.

### R2.1 No per-keystroke change signal exists — `didChange` has no real source *(priority: critical, BLOCKER for M2)*

**Description:** The current app publishes `DocumentModified` only on clean↔dirty transitions,
not on every editor text change. `EditorViewModel.NotifyTextChanged()` calls
`DocumentManager.MarkDirty()` which publishes `DocumentModified` on transitions only.
There is currently no event that fires on each keystroke.

**Required fix:** Add a new `DocumentTextChanged` message (or similar) that fires on every
editor text change, separate from dirty-state transitions. Route debounced LSP sync from
that signal. Keep `DocumentModified` for dirty UI only.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now explicitly
requires a new `DocumentTextChanged` message for LSP sync. Implementation remains pending.

### R2.2 Off-thread diagnostics vs. thread-affine UI — not addressed *(priority: critical, BLOCKER for M3)*

**Description:** `StreamJsonRpc` delivers `publishDiagnostics` on a background thread.
`TextDocument.Content` is thread-affine (throws off the UI thread). The Problems panel
`ObservableCollection` must be updated on the UI thread.

The codebase already has the pattern for this: `ShellViewModel`'s `StatusMessage` handler
uses `Dispatcher.UIThread` to marshal updates. The plan should require the same
dispatcher marshaling for diagnostic propagation and editor markers.

**Required fix:** Require `Dispatcher.UIThread` marshaling for all diagnostic updates
that touch UI-bound collections or editor markers.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now explicitly
requires UI-thread marshaling for diagnostics.

### R2.3 Workspace root ownership for `rootUri` is underspecified and risks an MVVM violation *(priority: high, BLOCKER for M1)*

**Description:** The plan says to use the `File → Open Folder` folder as `rootUri`, but the only
place the root is retained is `FileExplorerViewModel.RootPath` (a ViewModel). Per AGENTS rules,
`LSPManager` is a service and must not reference a ViewModel.

**Required fix:** `LSPManager` should subscribe to the existing `FolderOpened` message
(a service-safe record on the bus) and hold its own root state. Do not reach into
`FileExplorerViewModel`.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now
explicitly requires `LSPManager` to subscribe to `FolderOpened` for `rootUri`.

### R2.4 `CliWrap` discrepancy with `LIBRARIES.md` *(priority: medium)*

**Description:** `docs/LIBRARIES.md` lists Phase 4 as `+ StreamJsonRpc, CliWrap`,
and suggests CliWrap "for LSP spawning too." The plan adds only `StreamJsonRpc`
and uses raw `Process`. Raw `Process` is defensible for long-lived bidirectional
stdio servers, but the plan should reconcile the docs.

**Required fix:** Either note CliWrap is deferred to Phase 5, or justify raw `Process`
in the plan and update `LIBRARIES.md` accordingly.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan uses raw
`Process` for LSP spawning (better suited for long-lived bidirectional stdio).
`LIBRARIES.md` should be updated to note this divergence.

### R2.5 Internal contradiction on the DTO location *(priority: low)*

**Description:** Section 5.1 says diagnostic DTOs go in `src/Models/Languages/`,
while Section 7's file plan lists `src/Languages/Models/`. These are different
namespaces. Given `LanguageInfo` already lives in `src/Languages/`,
`src/Languages/Models/` is the consistent choice.

**Required fix:** Use `src/Languages/Models/` consistently.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now
uses `src/Languages/Models/` consistently.

### R2.6 LSP shutdown must hook the existing disposal path *(priority: medium)*

**Description:** The plan's M1 gate says the session must "shut down cleanly without
hanging," but doesn't connect to the app's teardown. `App.OnDesktopExit` disposes
the DI container, which disposes singletons.

**Required fix:** Register `LSPManager` as a singleton implementing `IDisposable` so it's
torn down on the existing path. Process kill must be bounded so it can't hang exit.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now
explicitly requires `LSPManager` to implement `IDisposable` and hook the existing
DI disposal path.

### R2.7 Bottom panel vs. the existing terminal placeholder *(priority: low)*

**Description:** `MainWindow.axaml` already has `Toggle Terminal` menu item and
`ShellViewModel.IsTerminalVisible`/`ToggleTerminalCommand` with no panel behind them.
Phase 4's Problems panel will sit where Phase 5 Output panel also wants to live.

**Required fix:** Implement the bottom region as a reusable container rather than
Problems-only, to avoid rework in Phase 5/8.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now
explicitly calls for a reusable bottom-panel container.

### R2.8 Completion UI should reuse the established view-bridge pattern *(priority: low)*

**Description:** The existing pattern is `EditorViewModel.FindReplaceRequested` (event) handled in
`EditorView.axaml.cs` against the live control. Completion should follow the same event-bridge seam
and can lean on AvaloniaEdit's built-in `CompletionWindow` rather than a hand-built overlay.

**Required fix:** Name the event-bridge pattern and AvaloniaEdit `CompletionWindow` as the preferred
completion UI seam.

**Status:** DESIGN DECIDED IN PLAN (2026-06-19) — the implementation plan now
explicitly names the event-bridge pattern and AvaloniaEdit `CompletionWindow` as the seam.

---

## Persistent Checks

Use these as the self-review checklist before closing Phase 4:

- [ ] `StreamJsonRpc` was added only after verifying it matches `docs/LIBRARIES.md`
- [ ] All new services are registered in `src/App.axaml.cs`
- [ ] All new public service methods have tests
- [ ] LSP process startup failures do not crash the app
- [ ] `didOpen` / `didChange` / `didClose` / `didSave` ordering is correct
- [ ] `didChange` uses full-document sync consistently in Phase 4
- [ ] Diagnostics are replaced, not accidentally accumulated forever, per file
- [ ] Closing a document clears its diagnostics from the Problems panel
- [ ] Active-file diagnostics are visibly rendered in the editor
- [ ] `Ctrl+Space` shows an observable completion UI result
- [ ] No `async void` was introduced outside Avalonia event handlers
- [ ] No static service access or service locator patterns were introduced
- [ ] `README.md` documents required LSP server installation/setup
- [ ] `dotnet build src/aero.csproj` passes
- [ ] `dotnet test tests` passes
- [ ] Manual Phase 4 smoke test passes
- [ ] `docs/phases/phase-4/TOFIX.md` has no open items before Phase 5 starts