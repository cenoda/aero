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

## Round 3 — Plan Review (2026-06-19)

Findings from the plan review against the live codebase and prior phases.

### R3.1 Entry Gate (M0) is under-specified *(priority: medium)*

**Description:** The plan states Phase 3 is the entry condition but does not list verifiable gates.

**Required fix:** Add a short M0 Entry Gate checklist with build/test/manual-smoke gates.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added M0 Entry Gate section with verification table.

### R3.2 Untitled documents and LSP *(priority: low)*

**Description:** `DocumentManager.NewDocument()` does not publish `DocumentOpened`. The plan says untitled docs "may remain local-only," but this should be explicit in the Definition of Done.

**Required fix:** Add explicit note that untitled documents remain local-only in Phase 4.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added untitled documents note to §5.1.

### R3.3 Files opened before a folder is opened *(priority: medium)*

**Description:** A user can open a `.cs` file without opening a folder. The plan says LSP is unavailable in that case, but the failure should be visible.

**Required fix:** Add a status-bar message "LSP disabled: open a folder first".

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added status-bar message note to §5.1.

### R3.4 LSP capability negotiation *(priority: medium)*

**Description:** The plan locks to full-document sync, but LSP servers advertise their supported sync kind. If `csharp-ls` advertises incremental-only, the client will misbehave.

**Required fix:** Add M1 gate item to read `textDocumentSync` from `initialize` response and assert full sync is supported.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added capability assertion to M1 gate.

### R3.5 StreamJsonRpc LSP payload types *(priority: low)*

**Description:** The plan says "keep DTOs small" but does not specify whether to use StreamJsonRpc LSP types or hand-rolled ones.

**Required fix:** State explicitly that Phase 4 uses hand-rolled minimal DTOs in `src/Languages/Models/`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added DTO strategy note to §5.2.

### R3.6 LSPManager size checkpoint *(priority: low)*

**Description:** `LSPManager` owns sessions, document routing, diagnostics, and completion. It risks becoming a god-class.

**Required fix:** Add a checkpoint at the end of M2: if `LSPManager` exceeds ~400–500 lines, open a TOFIX to extract `DiagnosticStore`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added size checkpoint to M2 gate.

### R3.7 Testing the JSON-RPC transport *(priority: medium)*

**Description:** The plan mentions "fake JSON-RPC peer" but gives no detail. This is high-value for M1 to avoid CI dependency on `csharp-ls`.

**Required fix:** Add a test that launches `LSPSession` against a small in-process `Process` that echoes JSON-RPC.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added mock JSON-RPC server test detail to §8.

### R3.8 docs/LIBRARIES.md update *(priority: medium)*

**Description:** The plan notes `CliWrap` should move from Phase 4 to Phase 5. This is a required doc change.

**Required fix:** Update `docs/LIBRARIES.md` timeline to reflect CliWrap as pre-positioned.

**Status:** ✅ RESOLVED (2026-06-19) — updated `docs/LIBRARIES.md` timeline to show CliWrap under Phase 5.

---

## Round 4 — Independent Review (2026-06-19)

Findings from independent review against the live codebase.

### R4.1 CliWrap already in csproj *(priority: low)*

**Description:** `CliWrap` is already present in `src/aero.csproj` (line 48) under Infrastructure.
The plan said it "lands in Phase 5" but it's already referenced — just not used.

**Required fix:** Reconcile wording: CliWrap is already referenced but not used in Phase 4;
LSP uses raw `Process`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — updated §4 to clarify CliWrap is pre-positioned but unused.

### R4.2 Singleton LSPManager disposal depends on eager instantiation *(priority: high, BLOCKER for M1)*

**Description:** `ServiceProvider` only disposes singletons it actually constructed. If `LSPManager`
self-subscribes to `FolderOpened` and nothing injects it, it's never instantiated — so neither
its subscription nor its `Dispose()` ever runs.

**Required fix:** Resolve `LSPManager` eagerly in `OnFrameworkInitializationCompleted` (like `ShellViewModel`)
to ensure it's constructed and subscribed before any folder is opened.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added eager resolution requirement to LSPManager Disposal section.

### R4.3 Thread-marshaling snippet not test-safe *(priority: medium)*

**Description:** The plan shows bare `Dispatcher.UIThread.Post(...)` which throws in headless unit tests.
The real codebase pattern uses `GetUiDispatcher()` with a null-check.

**Required fix:** Use the guarded pattern from `ShellViewModel.StatusMessage`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — updated Thread Safety section with test-safe pattern.

### R4.4 FolderOpened idempotency *(priority: medium)*

**Description:** `FolderOpened` fires on re-open or manual refresh of the same path. LSP session
(re)creation must be idempotent — don't spawn a second server for the same rootUri.

**Required fix:** Track active sessions by rootUri; reuse existing session if one already exists.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added idempotency risk and mitigation to §9.

---

## Round 5 — Act-On Review (2026-06-19)

Findings from act-on review.

### R5.1 Thread-affine Content capture for didChange *(priority: critical, BLOCKER for M2)*

**Description:** The Thread Safety section only covers incoming diagnostics marshaling.
It says nothing about the outgoing `didChange` path reading `doc.Content` on a background thread,
which will throw per `TextDocument`'s thread-affinity contract.

**Required fix:** Capture `doc.Content` synchronously on the UI thread in the `DocumentTextChanged`
handler, then schedule the background send.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added outgoing path to Thread Safety section.

### R5.2 DocumentOpened doesn't carry the document *(priority: critical, BLOCKER for M2)*

**Description:** `record DocumentOpened(string FilePath)` while `DocumentClosed`/`DocumentSaved` both carry
`TextDocument Document`. `LSPManager` can't build `didOpen` from a path alone without a lookup.

**Required fix:** Inject `DocumentManager` into `LSPManager` to look up documents by path.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added injection note to §5.1.

### R5.3 Extract DiagnosticStore for Phase 6 coupling *(priority: medium)*

**Description:** Phase 6 will need a shared sink for MSBuild error parsing. Bolting it on later is more work.

**Required fix:** Define a small `IDiagnosticStore` interface in M3 with `LSPManager` as the only writer for now.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added Phase 6 coupling note to §5.2.

### R5.4 Bottom-panel selector scope *(priority: medium)*

**Description:** Reusing `IsTerminalVisible` is a Phase 5/8 trap. Full `enum BottomPanelKind` + tabbed host edges into Phase 8 docking territory.

**Required fix:** Add `IsBottomPanelVisible` (separate from `IsTerminalVisible`) and leave kind/tab selector as Phase 5 concern.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added minimal approach note to Bottom Panel Reuse section.

### R5.5 Completion data flow *(priority: low)*

**Description:** The plan names the seam but not the payload return path.

**Required fix:** `EditorViewModel` holds `LSPManager`, exposes items via property + command, view binds to `CompletionWindow`.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added data flow note to Completion Seam section.

### R5.6 Version increment in send path *(priority: low)*

**Description:** Version numbers should increment on each sent change to avoid server skew.

**Required fix:** Add to M2 deliverables and gate.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added to M2 deliverables and gate.

### R5.7 URI normalization *(priority: low)*

**Description:** Add explicit `file://` absolute-URI rule + untitled behavior.

**Required fix:** Add to `TextDocument` model notes.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added to §5.2.

### R5.8 Completion fallback acceptance in M5 gate *(priority: low)*

**Description:** Tighten fuzzy gate with explicit fallback behavior.

**Required fix:** Show empty or "no suggestions" state, not silent failure.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added to M5 gate.

### R5.9 manual_test_phase4.sh *(priority: low)*

**Description:** Consistent with prior phases.

**Required fix:** Add to file plan and testing section.

**Status:** ✅ RESOLVED IN PLAN (2026-06-19) — added to §7 and §8.

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

---

## Round 6 — Plan Review Against Phases 0–3 (2026-06-19)

Findings from a final review of the implementation plan against the live codebase and the prior phases. See `docs/phases/phase-4/PLAN_REVIEW.md` for full rationale.

### R6.1 Update `docs/design/LSP_DESIGN.md` for Phase 4 constraints *(priority: medium)*

**Description:** `docs/design/LSP_DESIGN.md` shows incremental-or-full sync and a `DocumentOpened` record carrying `TextDocument`. The Phase 4 plan chooses full sync and the actual code uses `record DocumentOpened(string FilePath)`. The design doc is therefore inconsistent with both the plan and the codebase.

**Required fix:** Add a prominent note to `docs/design/LSP_DESIGN.md` stating that Phase 4 uses full-document sync and looks up documents via `DocumentManager`, or update the design doc diagrams/snippets.

**Status:** ✅ RESOLVED (2026-06-19) — added a "Phase 4 Constraints" section to `docs/design/LSP_DESIGN.md` and updated the sync snippets to reflect full-document sync and version-on-send semantics.

### R6.2 Verify `LanguageInfo.Id` values are valid LSP `languageId`s *(priority: medium, BLOCKER for M2)*

**Description:** The plan reuses `LanguageInfo.Id` from Phase 3's `ILanguageDetectionService` as the LSP `languageId` in `textDocument/didOpen`. This has not been explicitly verified. TextMate ids and LSP language ids often overlap (e.g., `"csharp"`), but they are not guaranteed to be identical for every language.

**Evidence:** The LSP specification uses VS Code/TextMate language identifiers as the de facto `languageId` values. The Phase 3 `LanguageDetectionService` ids (`"csharp"`, `"fsharp"`, `"json"`, `"xml"`, `"markdown"`, `"javascript"`, `"typescript"`, `"typescriptreact"`, `"javascriptreact"`, `"python"`, `"html"`, `"css"`, `"scss"`, `"yaml"`, `"sql"`, `"rust"`, `"go"`, `"java"`, `"cpp"`, `"c"`, `"shellscript"`, `"powershell"`, `"plaintext"`) are all standard identifiers. For Phase 4, only `"csharp"` needs end-to-end verification with `csharp-ls`; the rest can be verified per-language as additional servers are added.

**Required fix:** Document that `LanguageInfo.Id` is used directly as the LSP `languageId` and that Phase 4 verifies this only for C#.

**Status:** ✅ RESOLVED (2026-06-19) — `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` §5.2 updated with language-id note.

### R6.3 Define multi-folder / root replacement behavior *(priority: medium, BLOCKER for M1)*

**Description:** The plan says "one C# LSP session per opened folder," but does not specify what happens when a second folder is opened while a session already exists. Re-opening the same path is covered by idempotency, but a *different* path is not.

**Required fix:** Lock Phase 4 to a single active root. Opening a new folder should close the previous session and start a new one. Document this behavior.

**Status:** ✅ RESOLVED (2026-06-19) — `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` §5.1 updated with single-active-root rule.

### R6.4 Define back-fill behavior for already-open files when a folder is opened *(priority: medium, BLOCKER for M2)*

**Description:** A user can open individual `.cs` files before opening a folder. Once a folder is opened, `LSPManager` initializes a session, but it is unclear whether already-open documents are sent to the server via `didOpen`.

**Required fix:** Decide and document the behavior.

**Resolution:** Phase 4 will back-fill: when a session initializes, `LSPManager` scans `DocumentManager.Documents` and sends `didOpen` for every document that has a valid `file://` URI and a language supported by the active session. This is added to the M2 deliverables and gate.

**Status:** ✅ RESOLVED (2026-06-19) — `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` §5.1 and §6 M2 updated with back-fill requirement.

### R6.5 Verify AvaloniaEdit diagnostic marker / squiggle API *(priority: high, BLOCKER for M3)*

**Description:** The plan assumes an AvaloniaEdit marker-service seam for rendering diagnostics. AvaloniaEdit 11.3 may not expose the same `TextMarkerService` API that the WPF AvalonEdit did. If the API is unavailable or unstable, the M3 "red squigglies" gate cannot be met as written.

**Evidence:** Assembly metadata inspection of `AvaloniaEdit.dll` 11.3.0 confirms:
- No `TextMarkerService` type exists.
- No types containing "Squiggle" exist.
- The supported diagnostic-rendering seam is `AvaloniaEdit.Rendering.IBackgroundRenderer`, registered via `TextEditor.TextView.BackgroundRenderers`.
- Range tracking is supported via `AvaloniaEdit.Document.TextSegment` / `TextSegmentCollection<T>`.
- A reference implementation exists in `AvaloniaEdit.Search.SearchResultBackgroundRenderer`.

**Required fix:** Update the implementation plan to use `IBackgroundRenderer` instead of `TextMarkerService`, and implement the diagnostic renderer against that API in M3. The fallback remains line-level background highlight + Problems panel.

**Status:** ✅ RESOLVED (2026-06-19) — `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` §5.3 updated to use `IBackgroundRenderer`.

### R6.6 Handle Save As URI change in LSP sync *(priority: low-medium)*

**Description:** `SaveDocumentAsAsync` changes a document's `FilePath` and publishes `DocumentSaved`. The LSP server still has the old URI registered via `didOpen`. The plan does not state whether to send `didClose` for the old URI and `didOpen` for the new URI.

**Required fix:** Decide the behavior.

**Resolution:** Phase 4 will send `textDocument/didClose` for the old URI and `textDocument/didOpen` for the new URI after `SaveDocumentAsAsync` succeeds. This keeps the server's buffer identity in sync with the document path.

**Status:** ✅ RESOLVED (2026-06-19) — `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` §5.1 updated with Save As path-change rule.

---

## Round 7 — Scope Trim (2026-06-19)

After six review rounds, a deliberate simplification pass to keep Phase 4 aligned with its
"intentionally basic" charter. These are **reversals/reductions**, not new requirements. Do not
re-add them in a future round without a concrete, present need — they were removed on purpose.

### R7.1 Drop the `IDiagnosticStore` interface; keep a plain `DiagnosticStore` *(reduction)*

**Rationale:** R5.3 introduced an `IDiagnosticStore` interface + separate service "for Phase 6"
(two phases away). That is speculative generality. Phase 4 has exactly one writer (`LSPManager`).

**Change:** Use a plain concrete `DiagnosticStore` class (no interface, no `src/Languages/IDiagnosticStore.cs`).
If Phase 6 (MSBuild → Problems) adds a real second writer, extract the interface then.

**Status:** ✅ APPLIED — plan §5.1, §5.4, §6 M2, §7 updated.

### R7.2 Move back-fill of already-open files to a documented limitation *(reduction)*

**Rationale:** R6.4 added scan-and-`didOpen` of already-open files on `FolderOpened` — an edge case
(open file, then open its folder) that adds M2 logic beyond "basic."

**Change:** Documented as a Phase 4 limitation ("open the folder first") instead of implemented.

**Status:** ✅ APPLIED — removed from §5.1/§6 M2; added to "Phase 4 Limitations".

### R7.3 Move Save As URI swap to a documented limitation *(reduction)*

**Rationale:** R6.6 added `didClose`(old)+`didOpen`(new) on rename — an edge case that was also
buggy on the untitled→first-save path (no old URI to close). Removing it dodges that bug entirely.

**Change:** Documented as a Phase 4 limitation (server keeps old URI until close/reopen) instead of implemented.

**Status:** ✅ APPLIED — removed Save As rule from §5.1; added to "Phase 4 Limitations".

---

## Round 8 — Post-M2 Review / Deferred to M3 Real-Server Wiring (2026-06-19)

M1 and M2 are implemented and green (269/269). These three items surfaced during the M2
review. None block M2 — all are **real-server-dependent** and are intentionally deferred to
the M3 step where `csharp-ls` is installed and wired for real. Address them at the start of M3.

### R8.1 `LSPManager` blocks the publisher (UI) thread during session init *(priority: high, fix at M3 start)*

**Description:** `LSPManager.OnFolderOpened` calls
`newSession.InitializeAsync(...).GetAwaiter().GetResult()`. `FolderOpened` is published on the
UI thread, so once `csharp-ls` is actually installed, opening a folder will freeze the UI for the
full server cold-start handshake (potentially seconds). It is currently invisible only because
`csharp-ls` is absent and `StartProcess` throws fast. The M1 `ConfigureAwait(false)` prevents a
deadlock but not the freeze.

**Required fix:** Run session creation + `InitializeAsync` on a background task; assign `_session`
under lock once initialized. Documents opened during the init window remain unsynced (consistent
with the existing no-back-fill limitation). The existing tests already await the peer's init TCS,
so they are unaffected.

**Status:** OPEN — fix as the first step of M3.

### R8.2 Validate `csharp-ls` advertised sync kind vs the full-sync requirement *(priority: high, fix at M3 start)*

**Description:** Capability negotiation requires `textDocumentSync == Full` (1) or disables LSP.
Real `csharp-ls` commonly advertises **incremental** (2). If so, the current check disables LSP
entirely even though Phase 4 always sends full-document text.

**Required fix:** When `csharp-ls` is first run, observe its advertised `textDocumentSync`. Most
likely relax the check to accept incremental-or-full (Phase 4 still sends full text either way),
or document the actual behavior. Do not leave LSP silently disabled against the primary server.

**Status:** OPEN — validate against the real server at M3.

### R8.3 `TimeSpan` registered directly in DI *(priority: low)*

**Description:** `App.axaml.cs` registers a bare `TimeSpan` singleton for the `LSPManager` debounce
(`services.AddSingleton(typeof(TimeSpan), _ => 300ms)`). Injecting a primitive/struct type is
fragile — any other future consumer needing a `TimeSpan` would collide.

**Required fix:** Replace with a small options type (e.g., an `LspOptions` record carrying the
debounce interval) or construct `LSPManager` via a factory lambda that passes the literal.

**Status:** OPEN — low priority; clean up during M3 or later.
