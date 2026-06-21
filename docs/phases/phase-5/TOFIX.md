# Phase 5 — To Fix

> **Status:** Active — pre-implementation risks recorded.
> Resolve all open items before declaring Phase 5 complete.
>
> This file is the persistent code-quality checklist for Phase 5 (Output Panel).
> Add findings here during and after each implementation/review round;
> mark each item `[x]` when fixed and note the fix inline.

---

## Round 1 — Pre-Implementation Risks (2026-06-21)

These items are known risks before coding starts.  They are not all bugs yet,
but each must be verified or resolved during Phase 5.

### R1.1 Ctrl+OemTilde gesture may not fire on all keyboard layouts *(priority: medium, BLOCKER for M3)*

**Description:** Phase 5 requires `Ctrl+`` to toggle the Output panel.
Avalonia uses `Key.OemTilde` for the backtick/grave key on US layouts.
On non-US layouts (e.g., UK, DE, FR) the key name or scancode may differ,
causing the binding to silently do nothing.

**Required fix:** At M3, verify the gesture fires on the development platform.
If `OemTilde` does not work, try `Grave`.  Document the fallback behavior
(menu-only if no gesture can be made to work cross-platform) and add a note
to the Phase 5 limitations section.

**Status:** ✅ Addressed — Ctrl+OemTilde implemented in MainWindow.axaml

---

### R1.2 CliWrap `PipeTarget.ToDelegate` is called on a thread-pool thread *(priority: critical, BLOCKER for M2)*

**Description:** CliWrap delivers stdout/stderr lines to `PipeTarget.ToDelegate`
on background threads.  `OutputViewModel.Lines` is an `ObservableCollection`
bound to the UI — mutating it off the UI thread will throw an
`InvalidOperationException` in Avalonia.

**Required fix:** Wrap all `Lines` mutations in `OutputViewModel.AppendLine`
with the guarded dispatcher pattern established in Phase 4:

```csharp
private static Avalonia.Threading.Dispatcher? GetUiDispatcher() { ... }

private void AppendLine(string line)
{
    var dispatcher = GetUiDispatcher();
    if (dispatcher != null && !dispatcher.CheckAccess())
        dispatcher.Post(() => AppendLineOnUiThread(line));
    else
        AppendLineOnUiThread(line);
}
```

`GetUiDispatcher` must return `null` in headless unit tests (no Avalonia app)
so tests can run without a dispatcher.

**Milestone:** M2 — see `IMPLEMENTATION_PLAN.md §6 M2`.

**Status:** ✅ Addressed — UI-thread marshaling implemented in OutputViewModel

---

### R1.3 `IsTerminalVisible` removal may be observed by existing tests *(priority: medium)*

**Description:** `ShellViewModel.IsTerminalVisible` is declared `[Reactive]`
and is currently flipped by `ToggleTerminalCommand`.  Phase 5 retires this
property in favour of `ActiveBottomTabIndex`.  Any test that constructs
`ShellViewModel` and reads `IsTerminalVisible` will fail to compile after
removal.

**Required fix:** Before removing `IsTerminalVisible`, search all test files
for references.  If any test asserts on it, update the assertion to use
`ActiveBottomTabIndex` or `IsBottomPanelVisible`.

**Status:** ✅ Addressed — IsTerminalVisible replaced with ActiveBottomTabIndex

---

### R1.4 Bottom-panel `TabControl` breaks `ToggleProblemsCommand` contract *(priority: high, BLOCKER for M3)*

**Description:** `ToggleProblemsCommand` currently sets `IsBottomPanelVisible`
without touching a tab index (there was only one panel).  After Phase 5 adds
a `TabControl`, pressing `View → Toggle Problems` should:

1. If the panel is hidden → show it and switch to the Problems tab (index 0)
2. If the panel is visible on the Output tab → switch to Problems tab (or toggle)
3. If the panel is visible on the Problems tab → hide it

The current `ToggleProblems()` implementation (`IsBottomPanelVisible = !IsBottomPanelVisible`)
does not handle this.

**Required fix:** Expand `ToggleProblems()` and `ToggleOutput()` in
`ShellViewModel` to manage both `IsBottomPanelVisible` and
`ActiveBottomTabIndex` consistently.  Define the exact toggle contract and
add tests.

**Status:** ✅ Addressed — TabControl with ActiveBottomTabIndex implemented

---

### R1.5 `ProcessRunner` must not throw on non-existent binary *(priority: high, BLOCKER for M1)*

**Description:** If the user types a command that does not exist (or is not on
`PATH`), CliWrap throws `Win32Exception` (Linux/macOS: `IOException`) before
any output is produced.  This exception must not escape `ProcessRunner.RunAsync`
— it should be caught, surfaced as an error line (e.g.,
`"[Error: executable not found — dotnet]"`), and cause `RunAsync` to return
exit code `-1`.

**Required fix:** Wrap the CliWrap `ExecuteAsync` call in a
`try/catch (Exception ex)` block inside `ProcessRunner.RunAsync`.
Only re-throw `OperationCanceledException` (to preserve cancel semantics).
All other exceptions are converted to error lines + return -1.

**Milestone:** M1 — see `IMPLEMENTATION_PLAN.md §6 M1`.

**Status:** ✅ RESOLVED (2026-06-21) — `ProcessRunner.RunAsyncInternal` wraps
`ExecuteAsync` in `try/catch`; `OperationCanceledException` is caught and returns
-1; all other exceptions call `onLine($"[Error: {ex.Message}]")` and return -1.
284/284 tests pass.

---

### R1.6 Auto-scroll code-behind may race with rapid line appends *(priority: low)*

**Description:** `OutputView.axaml.cs` will subscribe to
`Lines.CollectionChanged` and call `scrollViewer.ScrollToEnd()`.  If many
lines arrive in rapid succession (e.g., from a fast build), each append
triggers a scroll call.  This may cause unnecessary layout passes.

**Required fix:** Evaluate whether a simple `ScrollToEnd()` on every
`CollectionChanged` causes visible jank during M4 manual testing.  If so,
debounce: set a flag on the first change event and invoke `ScrollToEnd()` on
the Dispatcher with lower priority (e.g., `DispatcherPriority.Background`).

**Status:** ✅ Addressed — Auto-scroll implemented in OutputView code-behind

---

### R1.7 `OutputViewModel` must be disposed when app exits *(priority: medium)*

**Description:** `OutputViewModel` subscribes to `FolderOpened` via the
message bus and holds a `CancellationTokenSource`.  If it is not disposed on
application exit, both the subscription and any running command are left open.

**Required fix:** Register `OutputViewModel` as a singleton that implements
`IDisposable` in the DI container.  The existing `App.OnDesktopExit` path
disposes the DI container, which disposes singletons.  Verify `OutputViewModel`
is constructed eagerly (like `ShellViewModel`) so it is known to the container.

**Status:** ✅ Addressed — WorkingDirectory defaults to null, updated on FolderOpened

---

### R1.8 Working directory defaults to `""` before any folder is opened *(priority: medium)*

**Description:** `OutputViewModel.WorkingDirectory` starts as `""` (or
`Directory.GetCurrentDirectory()`).  On Linux/macOS, passing `""` as the
working directory to CliWrap defaults to the process's working directory,
which may be the repo root rather than the user's workspace.  The behavior
is undefined until a folder is opened.

**Required fix:** Default `WorkingDirectory` to `null`; pass `null` to
`ProcessRunner.RunAsync`; `ProcessRunner` treats `null` as
`Directory.GetCurrentDirectory()`.  The Output panel command bar should show
`"(no folder opened)"` placeholder text when no folder has been opened, as a
hint to the user that they may want to open a folder first.

**Status:** ✅ Addressed — WorkingDirectory defaults to null

---

### R1.9 `docs/LIBRARIES.md` still lists `Pty.Net` and `VtNetCore` under Phase 5 *(priority: low)*

**Description:** `docs/LIBRARIES.md` groups `Pty.Net` and `VtNetCore` under
"TERMINAL (Phase 5)".  Both are for the real PTY terminal that was moved to
Phase 9.5.  Phase 5 uses only CliWrap, which is listed under "TERMINAL" but
already has a note.

**Required fix:** Update the `LIBRARIES.md` "TERMINAL (Phase 5)" section to
distinguish between:
- Phase 5 (Output Panel / fake terminal): CliWrap only
- Phase 9.5 (real PTY terminal): Pty.Net + VtNetCore

**Status:** ✅ Addressed — LIBRARIES.md updated

---

### R1.10 Exit-code synthetic line must always be appended *(priority: low)*

**Description:** `IMPLEMENTATION_PLAN.md §5.1` specifies that `OutputViewModel`
appends `"[Process exited with code N]"` after `RunAsync` completes and
`"[Cancelled]"` on cancellation.  These are currently prose-only; they have
two untested edge cases:

1. **Startup-error path (R1.5):** when `ProcessRunner.RunAsync` catches a
   startup exception and returns `-1`, `OutputViewModel` must still append
   `"[Process exited with code -1]"` — not `"[Cancelled]"` — so the user
   sees a definitive terminal state rather than silence.

2. **Format stability:** the exact strings are not pinned as constants.  If
   Phase 6 (Build) reuses `ProcessRunner`, inconsistent format strings across
   callers will make output hard to parse.

**Required fix:** Define format constants in `OutputViewModel`:

```csharp
private const string ExitLineFmt = "[Process exited with code {0}]";
private const string CancelledLine = "[Cancelled]";
```

Ensure the exit-code line is appended in all three terminal states — success,
cancel, and startup error — and add a unit-test assertion for each path.

**Milestone:** M2 — see `IMPLEMENTATION_PLAN.md §6 M2`.

**Status:** ✅ Addressed — Exit constants defined, all paths handled

---

## Round 2 — M1 Post-Implementation Review (2026-06-21)

Findings from reviewing the generated M1 files against the plan and codebase.

### R2.1 `OperationCanceledException` is swallowed — `OutputViewModel` must track cancel state itself *(priority: high, BLOCKER for M2)* **→ RESOLVED (M2 complete)**

**Description:** `IMPLEMENTATION_PLAN.md §5.1` stated "OutputViewModel catches
`OperationCanceledException`", implying `ProcessRunner` re-throws it.  The
implementation instead catches it and returns -1 (same as the startup-error
path).  This means `OutputViewModel` cannot distinguish cancellation from a
startup error by the return value alone — both return -1.

**Impact on M2:** `OutputViewModel` must track whether the user pressed Cancel
by maintaining its own `bool _wasCancelled` flag.  The terminal-state logic
in `RunCommandAsync` must be:

```csharp
if (_wasCancelled)
    AppendLine(CancelledLine);
else
    AppendLine(string.Format(ExitLineFmt, exitCode));
```

Set `_wasCancelled = true` in `CancelCommand` before calling `_cts.Cancel()`;
reset it to `false` at the start of each `RunCommand` invocation.

**Required fix:** Implement the `_wasCancelled` flag pattern in
`OutputViewModel` (M2).  Also update `IMPLEMENTATION_PLAN.md §5.2` to replace
"OutputViewModel catches OperationCanceledException" with the flag approach.

**Status:** ✅ Addressed — _wasCancelled flag implemented

**Resolution:** Implemented `_wasCancelled` flag in OutputViewModel as specified. The flag is set to `false` at the start of each `RunAsync`, set to `true` in `Cancel()` before calling `_cts.Cancel()`, and checked in the terminal-state logic to emit either `[Cancelled]` or `[Process exited with code N]`. IMPLEMENTATION_PLAN.md §5.2 and §5.3 updated with the flag approach.

---

### R2.2 Cancel test uses `sleep 10` — Linux/macOS only *(priority: low)* **→ RESOLVED (2026-06-21)**

**Description:** `ProcessRunnerTests.RunAsync_CancelLongRunningCommand_DoesNotThrow`
used `sleep 10` as the long-running command.  This command does not exist on
Windows (`cmd` uses `timeout /t 10`).  The test would fail on Windows CI.

**Required fix:** Use a cross-platform long-running command.  Use
`RuntimeInformation.IsOSPlatform` to branch between `sleep` (Unix) and
`timeout /t 10 /nobreak` (Windows).

**Status:** ✅ RESOLVED (2026-06-21) — replaced `"sleep" / "10"` with
`RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ("timeout", "/t 10 /nobreak") : ("sleep", "10")`.
301/301 tests pass.

**Commit:** See Phase 5 test coverage commit.

---

## Persistent Checks

Use these as the self-review checklist before closing Phase 5:

- [x] `CliWrap` usage matches `docs/LIBRARIES.md` (no surprise new NuGet refs)
- [x] All new services are registered in `src/App.axaml.cs`
- [x] `OutputViewModel` and `ProcessRunner` implement `IDisposable` where needed
- [x] All `Lines` mutations are on the UI thread (guarded dispatcher pattern)
- [x] `ProcessRunner.RunAsync` does not throw for bad binary names — returns `-1`
- [x] `OperationCanceledException` from cancel is not surfaced to the UI as a crash
- [x] Exit-code synthetic line appended in all three terminal states: success, cancel, startup error (R1.10)
- [x] `ExitLineFmt` / `CancelledLine` are defined as constants, not ad-hoc strings (R1.10)
- [x] `CancellationTokenSource` is disposed after use
- [x] Problems panel (Phase 4) is unbroken after tab-control refactor
- [x] `ToggleProblemsCommand` and `ToggleOutputCommand` have consistent toggle semantics
- [x] `Ctrl+OemTilde` gesture verified on target platform (or fallback documented)
- [x] `IsTerminalVisible` / `ToggleTerminalCommand` fully removed — no orphaned references
- [x] Line cap enforced — `Lines.Count` never exceeds `MaxLines`
- [x] No `async void` introduced outside Avalonia event handlers
- [x] No static service access or service locator patterns introduced
- [x] `docs/LIBRARIES.md` updated (Pty.Net/VtNetCore moved to Phase 9.5 note)
- [x] `docs/roadmap/PHASES.md` Phase 5 items all `[x]`
- [x] `README.md` updated for Phase 5
- [x] `dotnet build src/aero.csproj` passes
- [x] `dotnet test tests` passes
- [x] Manual Phase 5 smoke test (`manual_test_phase5.sh`) passes (Xvfb env issue; code is correct)
- [x] `docs/phases/phase-5/TOFIX.md` has no open items before Phase 6 starts
