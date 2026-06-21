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

**Status:** Open

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

**Status:** Open

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

**Status:** Open

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

**Status:** Open

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

**Status:** Open

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

**Status:** Open (evaluate at M4)

---

### R1.7 `OutputViewModel` must be disposed when app exits *(priority: medium)*

**Description:** `OutputViewModel` subscribes to `FolderOpened` via the
message bus and holds a `CancellationTokenSource`.  If it is not disposed on
application exit, both the subscription and any running command are left open.

**Required fix:** Register `OutputViewModel` as a singleton that implements
`IDisposable` in the DI container.  The existing `App.OnDesktopExit` path
disposes the DI container, which disposes singletons.  Verify `OutputViewModel`
is constructed eagerly (like `ShellViewModel`) so it is known to the container.

**Status:** Open

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

**Status:** Open

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

**Status:** Open

---

## Persistent Checks

Use these as the self-review checklist before closing Phase 5:

- [ ] `CliWrap` usage matches `docs/LIBRARIES.md` (no surprise new NuGet refs)
- [ ] All new services are registered in `src/App.axaml.cs`
- [ ] `OutputViewModel` and `ProcessRunner` implement `IDisposable` where needed
- [ ] All `Lines` mutations are on the UI thread (guarded dispatcher pattern)
- [ ] `ProcessRunner.RunAsync` does not throw for bad binary names — returns `-1`
- [ ] `OperationCanceledException` from cancel is not surfaced to the UI as a crash
- [ ] `CancellationTokenSource` is disposed after use
- [ ] Problems panel (Phase 4) is unbroken after tab-control refactor
- [ ] `ToggleProblemsCommand` and `ToggleOutputCommand` have consistent toggle semantics
- [ ] `Ctrl+OemTilde` gesture verified on target platform (or fallback documented)
- [ ] `IsTerminalVisible` / `ToggleTerminalCommand` fully removed — no orphaned references
- [ ] Line cap enforced — `Lines.Count` never exceeds `MaxLines`
- [ ] No `async void` introduced outside Avalonia event handlers
- [ ] No static service access or service locator patterns introduced
- [ ] `docs/LIBRARIES.md` updated (Pty.Net/VtNetCore moved to Phase 9.5 note)
- [ ] `docs/roadmap/PHASES.md` Phase 5 items all `[x]`
- [ ] `README.md` updated for Phase 5
- [ ] `dotnet build src/aero.csproj` passes
- [ ] `dotnet test tests` passes
- [ ] Manual Phase 5 smoke test (`manual_test_phase5.sh`) passes
- [ ] `docs/phases/phase-5/TOFIX.md` has no open items before Phase 6 starts
