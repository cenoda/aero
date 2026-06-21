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

**Status:** ☐ Open

---

### R1.2 No public API to stream external output through `OutputViewModel` *(priority: high, BLOCKER for M3)*

**Description:** `OutputViewModel` can only run from its `CommandText` bar; `AppendLine` is
private. The build needs to reuse its `Lines`/marshaling/cancel/exit-line/line-cap machinery.

**Required fix:** Extract the shared run core from the existing `RunAsync` and expose
`RunExternalAsync(executable, arguments, workingDir, ct)`. Honor the single-active-command
rule (`IsRunning`). See plan §5.5.

**Status:** ☐ Open

---

### R1.3 Build output may exceed `MaxLines` and corrupt error parsing *(priority: high, BLOCKER for M4)*

**Description:** `OutputViewModel.Lines` is capped at 10k (FIFO trim). Parsing errors from the
trimmed `Lines` collection would miss early diagnostics on a verbose build.

**Required fix:** Parse from the coordinator's own captured buffer (the `onLine` callback both
appends to the panel *and* buffers for parsing), never from `OutputViewModel.Lines`. See plan §5.6.

**Status:** ☐ Open

---

### R1.4 MSBuild parser must match the real, verified format *(priority: high)*

**Description:** Error-format regexes written from memory drift. The real format on this machine
(`dotnet 9.0.117`) is:
`/abs/File.cs(line,col): error CSxxxx: message [project]` (also `warning`).

**Required fix:** Use the §5.2 regex; strip the trailing ` [project]`; treat line/col as 1-based;
ignore lines without a `file(line,col)` prefix (documented limitation). Tests must assert against
these exact captured strings, not invented ones.

**Status:** ☐ Open

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

**Status:** ☐ Open

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
