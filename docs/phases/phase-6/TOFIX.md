# Phase 6 — To Fix

> **Status:** Active — pre-implementation risks recorded.
> Resolve all open items before declaring Phase 6 complete.
>
> Persistent code-quality checklist for Phase 6 (Build & Output).
> Add findings during/after each implementation/review round; mark `[x]` when fixed
> and note the fix inline. Do not start Phase 7 while any item is unchecked.

---

## Round 1 — Pre-Implementation Risks (2026-06-21)

### R1.1 Build diagnostics must not clobber LSP diagnostics *(priority: critical, BLOCKER for M4)*

**Description:** `DiagnosticStore` is keyed by `fileUri` only and documented "one writer:
LSPManager". `SetDiagnostics(uri, list)` replaces *all* diagnostics for that uri. If the build
writes errors for a `.cs` file that also has LSP diagnostics, one source erases the other.

**Required fix:** Add a `source` dimension (`SetDiagnostics(source, uri, list)`,
`ClearDiagnostics(source, uri)`, `ClearSource(source)`), key internally by `(source, uri)`,
merge in `GetAllDiagnostics`. Update `LSPManager` to use `source:"lsp"`; build uses
`source:"build"`. Add a coexistence test. **Do not** introduce an `IDiagnosticStore` interface
(no third writer yet — YAGNI). See plan §5.4.

**Status:** ☑ Closed (DiagnosticStore uses (source, uri) key, LSPManager passes "lsp", ShellViewModel passes "build") [M4]

---

### R1.2 No public API to stream external output through `OutputViewModel` *(priority: high, BLOCKER for M3)*

**Description:** `OutputViewModel` can only run from its `CommandText` bar; `AppendLine` is
private. The build needs to reuse its `Lines`/marshaling/cancel/exit-line/line-cap machinery.

**Required fix:** Extract the shared run core from the existing `RunAsync` and expose
`RunExternalAsync(executable, arguments, workingDir, ct)`. Honor the single-active-command
rule (`IsRunning`). See plan §5.5.

**Status:** ☑ Closed (ShellViewModel.BuildCommand calls OutputViewModel.RunExternalAsync) [2847722]

---

### R1.3 Build output may exceed `MaxLines` and corrupt error parsing *(priority: high, BLOCKER for M4)*

**Description:** `OutputViewModel.Lines` is capped at 10k (FIFO trim). Parsing errors from the
trimmed `Lines` collection would miss early diagnostics on a verbose build.

**Required fix:** Parse from the coordinator's own captured buffer (the `onLine` callback both
appends to the panel *and* buffers for parsing), never from `OutputViewModel.Lines`. See plan §5.6.

**Status:** ☑ Closed (M1 tests verify — captured via `onLine` callback, not `OutputViewModel.Lines`) [2847722]

---

### R1.4 MSBuild parser must match the real, verified format *(priority: high)*

**Description:** Error-format regexes written from memory drift. The real format on this machine
(`dotnet 9.0.117`) is:
`/abs/File.cs(line,col): error CSxxxx: message [project]` (also `warning`).

**Required fix:** Use the §5.2 regex; strip the trailing ` [project]`; treat line/col as 1-based;
ignore lines without a `file(line,col)` prefix (documented limitation). Tests must assert against
these exact captured strings, not invented ones.

**Status:** ☑ Closed (M1 tests verify — `ParseErrors_ParsesErrorLine`, `_ParsesWarningLine`, `_StripsTrailingProjectBracket`) [fb3b45c]

---

### R1.5 Localized / non-English MSBuild output *(priority: medium)*

**Description:** The parser keys on the literal `error`/`warning` words. A localized .NET SDK emits
translated severity words and the parser silently finds nothing.

**Required fix:** Anchor the regex on the `CSxxxx`/`[A-Za-z]+\d+` code shape as the primary signal;
document English-locale output as a Phase 6 limitation. Re-evaluate (forcing invariant culture) only
if it bites in practice — do not gold-plate now.

**Status:** ☐ Open

---

### R1.6 Caret navigation races the editor resubscribe *(priority: medium)*

**Description:** `EditorView` rebinds the active `TextEditor` at `DispatcherPriority.Loaded` after a
tab switch. Setting the caret immediately after `OpenFileAsync` may target an editor that isn't bound
yet, so the jump silently no-ops.

**Required fix:** Set caret/scroll via the `NavigationRequested` event handled in code-behind, posted
at `Loaded` priority after `OpenFileAsync` completes (mirror the existing resubscribe pattern).
Accept best-effort behavior on rapid re-clicks and document it. See plan §5.7 / risk R6.4.

**Status:** ☐ Open

---

### R1.7 Graceful failure when `dotnet` is absent *(priority: medium)*

**Description:** `DotNetBuildService` assumes `dotnet` is on `PATH`. `ProcessRunner` returns `-1` and
emits `[Error: …]`, but the build flow must surface a clear status, not a silent failure.

**Required fix:** On exit `-1` with no parsed errors, set status `Build failed: dotnet not found (or
crashed)`. Keep the `[Error: …]` line visible in the Output panel.

**Status:** ☑ Closed (ShellViewModel checks for exitCode==-1 with "not found" in output, sets status "Build failed: dotnet not found") [M5]

---

### R1.8 `BuildFinished.Output` must not duplicate streamed output *(priority: low)*

**Description:** `BuildFinished(int ExitCode, string Output)` exists. Passing the full multi-MB build
log here duplicates what already streamed to the Output panel and bloats the message.

**Required fix:** Publish `BuildFinished` with an empty or short summary string; the authoritative
output lives in the Output panel.

**Status:** ☐ Open

---

## Persistent Checks (self-review before closing Phase 6)

- [ ] Only `DotNetBuildService` implemented — no speculative Npm/Cargo/Make services (YAGNI)
- [ ] `DotNetBuildService` uses injected `IProcessRunner` — no direct `Process`/`CliWrap`
- [ ] No new NuGet packages added (build uses existing CliWrap via IProcessRunner)
- [ ] `IBuildService` drops README's redundant `StreamOutputAsync` (deviation recorded in plan §4)
- [ ] Build & LSP diagnostics coexist for the same file (R1.1) with a test
- [ ] `ClearSource("build")` runs before each build so stale errors don't accumulate
- [ ] Error parsing uses the captured buffer, not `OutputViewModel.Lines` (R1.3)
- [ ] Parser tested against the real verified MSBuild format (R1.4)
- [ ] Single active build enforced via `IsRunning` (R6.6)
- [ ] `Ctrl+Shift+B` builds; output streams into the Output tab
- [ ] Click problem → opens file at line/col (best-effort, R1.6)
- [ ] Status bar shows Building…/succeeded/failed
- [ ] No `async void` outside Avalonia event handlers; no static service access
- [ ] All new services registered in `src/App.axaml.cs`; eager-resolve only if a message subscriber
- [ ] `dotnet build src/aero.csproj` passes
- [ ] `dotnet test tests` passes (302 existing + new)
- [ ] `manual_test/manual_test_phase6.sh` passes
- [ ] `docs/roadmap/PHASES.md` Phase 6 items all `[x]`; `README.md` updated
- [ ] `docs/phases/phase-6/TOFIX.md` has no open items before Phase 7 starts

---

## Round 2 — Post-Implementation Review (2026-06-21)

Findings from reviewing the implemented Phase 6 against the plan and live code.
Build is clean; **317/317 tests pass**. The items below are real and block closing the phase.

### R2.1 Build diagnostics are 1-based but `Diagnostic.TextRange` is 0-based *(priority: critical, BLOCKER)*

**Description:** `ShellViewModel.BuildAsync` maps parsed errors with
`new TextRange(e.Line, e.Column, e.Line, e.Column)` using the **raw 1-based** MSBuild
values. But the `Diagnostic.TextRange` contract is **0-based**:
- `EditorDiagnosticRenderer` does `docLineNumber = diag.Range.StartLine + 1` ("LSP uses 0-based").
- `Diagnostic.LocationText` does `Ln {StartLine + 1}, Col {StartCharacter + 1}`.
- `LSPManager` writes 0-based ranges.

Consequences: build errors display one line too high in the Problems panel (`Ln N+1`),
the editor highlights the wrong line, and the two diagnostic sources are inconsistent.
Plan §5.4 and R1.4 explicitly required `TextRange(Line-1, Column-1, …)`.

**Required fix:** In the build→diagnostic mapping subtract 1:
`new TextRange(e.Line - 1, e.Column - 1, e.Line - 1, e.Column - 1)` (guard against negatives).
Add a test asserting a parsed `error CSxxxx` on line 5 yields `Range.StartLine == 4`.

**Status:** ☐ Open

### R2.2 Navigation uses the wrong line/column base → LSP jumps to the wrong line *(priority: high, BLOCKER)*

**Description:** `ProblemsViewModel.NavigateToDiagnostic` publishes
`NavigateToLocation(uri, Range.StartLine, Range.StartCharacter)` (0-based by contract).
`EditorViewModel.OpenFileAndNavigateAsync` then calls `tab.Document.GetOffset(line, column)`
with a comment claiming "1-based". `GetOffset` is 1-based (inverse of `GetLineColumn`), so a
0-based range lands one line short (and line 0 is invalid). It only "works" today for build
because build currently stores 1-based (see R2.1) — i.e. two bugs cancel for build and expose
the bug for LSP.

**Required fix:** Once R2.1 makes ranges 0-based for all sources, convert in navigation:
`GetOffset(line + 1, column + 1)` with bounds clamping. Add a navigation test.

**Status:** ☐ Open

### R2.3 `IBuildService.BuildAsync` is bypassed in the real flow *(priority: high, BLOCKER)*

**Description:** `ShellViewModel.BuildAsync` calls
`_outputViewModel.RunExternalAsync("dotnet", "build", _workspacePath, …)` with a **hardcoded**
executable/arguments, then only calls `_buildService.ParseErrors(...)`. `IBuildService.BuildAsync`,
`BuildOptions`, `BuildResult`, `BuildArguments` (`/clp:NoSummary`, `--no-incremental`, `TargetPath`)
are never invoked in production — the factory is used only as a yes/no "is there a build system"
gate. This defeats the abstraction-first design (AGENTS.md §4) and the M1/M3 intent: the service
is dead code outside unit tests.

**Required fix:** Route the build through `_buildService.BuildAsync(options, onLine, ct)`, passing
an `onLine` that appends to the Output panel; use the returned `BuildResult` for exit code +
parsed errors. Add a `BuildCommand` test asserting it drives `IBuildService` (stub).

**Status:** ☐ Open

### R2.4 Exit code & errors are scraped from the capped `OutputViewModel.Lines` *(priority: high, BLOCKER — R1.3/R6.5)*

**Description:** Because `RunExternalAsync` returns `Task` (no exit code), `BuildAsync` recovers
the exit code by string-matching `"exited with code"` on the **last line** of
`_outputViewModel.Lines`, and parses errors from `_outputViewModel.Lines.ToList()` — the
10k-FIFO-capped UI collection. A verbose build (>10k lines) loses early diagnostics, and the
exit-code scrape is fragile. This is exactly what R1.3/R6.5 warned against.

**Required fix:** Take exit code and errors from `BuildResult` (the service captures its own buffer,
per plan §5.6). Do not parse from `OutputViewModel.Lines`.

**Status:** ☐ Open

### R2.5 No single-active-build guard *(priority: medium — R6.6)*

**Description:** `BuildCommand` does not check `_outputViewModel.IsRunning`. Triggering a build
while a command (or build) is already running overwrites `OutputViewModel._cts`, so the in-flight
operation's `finally` disposes the new CTS and flips `IsRunning` — a concurrency hazard.

**Required fix:** No-op with a status message when `_outputViewModel.IsRunning` (or add a
`canExecute` guard on `BuildCommand`).

**Status:** ☐ Open

### R2.6 Warnings on a successful build are dropped *(priority: medium)*

**Description:** Error parsing/population is gated by `if (exitCode != 0)`. A build that succeeds
with warnings (exit 0) never populates the Problems panel.

**Required fix:** Always parse and publish diagnostics (errors and warnings) regardless of exit code.

**Status:** ☐ Open

### R2.7 `ProblemsViewModel` updates twice per change *(priority: low / cleanup)*

**Description:** `ProblemsViewModel` subscribes to **both** the bus `DiagnosticsUpdated` message and
the new `DiagnosticStore.DiagnosticsUpdated` event, and `DiagnosticStore.PublishDiagnosticsUpdated`
fires both. The list rebuilds twice per change (idempotent but wasteful). The direct event was not
in the plan (the panel was to stay on the bus path) — scope creep.

**Required fix:** Pick one path. Simplest: drop the `DiagnosticStore.DiagnosticsUpdated` event +
its subscription and keep the existing message-bus flow.

**Status:** ☐ Open

### R2.8 Build `CancellationTokenSource` leaked; no cancel path *(priority: low)*

**Description:** `BuildAsync` creates `var cts = new CancellationTokenSource();` that is never
disposed and never cancelled (no UI/keybinding). The `catch (OperationCanceledException)` is
unreachable as written.

**Required fix:** Dispose the CTS (`using`/`finally`), or wire a real cancel (e.g. reuse the Output
panel Cancel). If cancel is out of scope for Phase 6, drop the dead CTS/catch and document it.

**Status:** ☐ Open

### R2.9 Manual test mutates the real source tree *(priority: low)*

**Description:** `manual_test_phase6.sh` writes `src/TestCompileError.cs` and builds the real
`src/aero.csproj` (temporarily breaking the app's own build) before deleting it. If the script is
interrupted, a broken file is left in `src/`.

**Required fix:** Build a throwaway temp project (like the planning probe in `/tmp`) instead of
polluting `src/`.

**Status:** ☐ Open

### R2.10 PHASES.md marked complete prematurely *(priority: medium / process)*

**Description:** All Phase 6 boxes in `docs/roadmap/PHASES.md` are `[x]`, but R2.1–R2.4 mean
"populate Problems panel" and "click error → jump to file/line" are not correct end-to-end.

**Required fix:** Leave Phase 6 items unchecked (or note in-progress) until R2.1–R2.4 are resolved
and re-verified. Per `plan-rules` §5, gates must be verifiable.

**Status:** ☐ Open

### R2.11 Test coverage gap *(priority: medium)*

**Description:** 317 tests pass but none caught R2.1 (off-by-one) or R2.3 (bypassed `BuildAsync`).

**Required fix:** Add tests: build `ParsedError`→`Diagnostic` is 0-based; `BuildCommand` invokes
`IBuildService.BuildAsync`; navigation lands on the correct line for a 0-based range.

**Status:** ☐ Open
