# Phase 5 — Implementation Plan

> **Phase:** 5 — Output Panel (Fake Terminal)
> **Date:** 2026-06-21
> **Status:** Ready for implementation

---

## 1. Goal

Add a reusable **Output panel** that runs external commands and streams their
stdout/stderr in real time.  No PTY, no interactive shell — text output only.

Key deliverables:

- `IProcessRunner` / `ProcessRunner` — CliWrap-based command runner with
  `CancellationToken` support; reusable for Phase 6 (Build) and Phase 7 (Git)
- `OutputViewModel` / `OutputView` — scrollable, clearable output panel
- Bottom panel extended to host both **Problems** and **Output** tabs
- `Ctrl+`` toggles the Output tab and opens the bottom panel if hidden
- Cancel button aborts in-flight commands

This phase is intentionally **basic**: the runner accepts a single command +
arguments string and streams plain text.  Interactive prompts, ANSI colors,
and PTY support are out of scope (see Phase 9.5).

---

## 2. Entry Check

Phase 5 may begin because:

- Phases 0–4 checklist items are all `[x]` in `docs/roadmap/PHASES.md`
- `docs/phases/phase-4/TOFIX.md` has no open items
- **CliWrap** is already referenced in `src/aero.csproj` (pre-positioned in
  Phase 4 but unused — see Phase 4 TOFIX R4.1)
- The bottom panel host (`IsBottomPanelVisible`, rows 1–2 in the editor
  column `Grid`) was built in Phase 4 with the Phase 5 tab-selector extension
  explicitly deferred (Phase 4 TOFIX R5.4)

Open gaps before implementation:

- `src/Terminal/` directory exists but contains only `.gitkeep` — no code yet
- Bottom panel currently shows only `ProblemsView` with no tab selector
- No `OutputViewModel` or `OutputView` exist
- `ToggleTerminalCommand` in `ShellViewModel` flips `IsTerminalVisible` but
  that flag is not bound to any view

---

## M0 — Entry Gate

Before writing Phase 5 code, verify these gates:

| Gate | Verification |
|------|--------------|
| Phase 4 checklist complete | `docs/roadmap/PHASES.md` Phase 4 items all `[x]` |
| Build passes | `dotnet build src/aero.csproj` succeeds with 0 errors |
| Tests pass | `dotnet test tests` passes (all green) |
| Manual smoke passes | `manual_test_phase4.sh` passes |
| No Phase 4 blockers | `docs/phases/phase-4/TOFIX.md` has no open items |

---

## 3. Scope

### In Scope

1. `IProcessRunner` / `ProcessRunner` — CliWrap wrapper (stdout + stderr, exit
   code, CancellationToken, working directory, line-by-line streaming)
2. `OutputViewModel` — command input, run/cancel/clear commands, line
   collection, running state
3. `OutputView` — scrollable output list, toolbar
4. Bottom panel refactored to host **Problems** and **Output** as sibling tabs
5. `Ctrl+`` key binding toggles Output tab (opens panel if hidden)
6. `View → Toggle Output` menu item
7. Auto-scroll to bottom as lines arrive
8. Line count cap to prevent unbounded memory growth
9. Exit code reported as the last line of output
10. `manual_test/manual_test_phase5.sh`

### Out of Scope

- ANSI / VT100 color escape code rendering
- Interactive input (stdin)
- PTY (pseudo-terminal) — deferred to Phase 9.5
- Multiple concurrent command instances
- Persistent command history across sessions
- Shell integration (no `$SHELL`, no shell builtins)
- Integrating the runner with Build commands (Phase 6) or Git (Phase 7)
  — the *service* is designed for reuse, but Phase 5 wires only the
  Output panel command bar

---

## 4. Dependency Decision

### CliWrap — Already Referenced

`CliWrap` (v3.*) is already present in `src/aero.csproj` under the
Infrastructure group (pre-positioned in Phase 4).  No new NuGet package
reference is needed.

Key CliWrap APIs used in Phase 5:

```csharp
await Cli.Wrap(executable)
    .WithArguments(args)
    .WithWorkingDirectory(dir)
    .WithValidation(CommandResultValidation.None)   // don't throw on non-zero exit
    .WithStandardOutputPipe(PipeTarget.ToDelegate(onStdout))
    .WithStandardErrorPipe(PipeTarget.ToDelegate(onStderr))
    .ExecuteAsync(cancellationToken);
```

`CommandResultValidation.None` is required: build tools and git routinely
return non-zero exit codes without it being an error.

### No New Dependencies

`Pty.Net` and `VtNetCore` (listed in `docs/LIBRARIES.md` under Phase 5) apply
only to the real PTY terminal deferred to Phase 9.5.  Phase 5 uses only
CliWrap.  Update `docs/LIBRARIES.md` to clarify this split.

---

## 5. Architecture

### 5.1 ProcessRunner (Service Layer)

Location: `src/Terminal/`

```csharp
// src/Terminal/IProcessRunner.cs
namespace Aero.Terminal;

public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="executable"/> with <paramref name="arguments"/>
    /// and streams each stdout/stderr line to <paramref name="onLine"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    Task<int> RunAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken = default);
}
```

`ProcessRunner` implements this with CliWrap.  Both stdout and stderr are
piped to `onLine` (interleaved order is not guaranteed, which is acceptable
for Phase 5).

**Thread safety:** CliWrap invokes `onLine` on thread-pool threads.
`OutputViewModel` is responsible for marshaling to the UI thread.

**Exit-code reporting:** `ProcessRunner.RunAsync` returns the integer exit
code.  `OutputViewModel` appends a synthetic line such as
`"\n[Process exited with code 0]"` after the task completes.

**Cancellation:** Passing a cancelled `CancellationToken` causes CliWrap to
kill the child process.  `OperationCanceledException` is caught *inside
`ProcessRunner`* and returns -1 (see TOFIX R2.1).  `OutputViewModel` detects
cancellation via its own `_wasCancelled` flag (set in `CancelCommand` before
`_cts.Cancel()`) and appends `CancelledLine` when the flag is set, or
`string.Format(ExitLineFmt, exitCode)` otherwise.

### 5.2 OutputViewModel

Location: `src/ViewModels/OutputViewModel.cs`

```
OutputViewModel
  ├── CommandText : string       — editable command + args bar
  ├── WorkingDirectory : string  — set from FolderOpened; "" = cwd
  ├── IsRunning : bool           — true while command is in flight
  ├── Lines : ObservableCollection<string>  — all output lines
  ├── RunCommand  (canExecute = !IsRunning && CommandText.Length > 0)
  ├── CancelCommand (canExecute = IsRunning)
  └── ClearCommand
```

**Line count cap:** When `Lines.Count` exceeds `MaxLines` (default 10 000),
remove lines from the front (FIFO) before appending new ones.  This prevents
memory runaway on verbose commands.

**Thread safety:** All `Lines` mutations happen on the UI thread via the
guarded dispatcher pattern from Phase 4:

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

**FolderOpened:** `OutputViewModel` subscribes to `FolderOpened` to keep
`WorkingDirectory` current, so commands run in the workspace root by default.
This mirrors the `LSPManager` pattern.

**Disposal:** `OutputViewModel` implements `IDisposable`.  The `_cts` is
cancelled and disposed; the message bus subscription is unsubscribed.

**Terminal-state lines:** OutputViewModel tracks whether the user pressed Cancel
by maintaining a `_wasCancelled` flag (per TOFIX R2.1).  After the process
completes, this flag determines whether to emit `[Cancelled]` or
`[Process exited with code N]`.  The flag is reset to `false` at the
start of each `RunCommand` invocation and set to `true` in `Cancel()`
before calling `_cts.Cancel()`.

### 5.3 OutputView

Location: `src/Views/OutputView.axaml`

```
OutputView (UserControl)
  ├── Header bar (DockPanel.Dock="Top")
  │   ├── "Output" label
  │   └── Toolbar: [CommandText TextBox] [Run ▶] [Cancel ■] [Clear ✕]
  └── ScrollViewer (fills remaining space)
      └── ItemsControl bound to OutputViewModel.Lines
          └── DataTemplate: TextBlock (monospace font)
```

**Auto-scroll:** Avalonia `ScrollViewer` does not auto-scroll on collection
changes.  Implement in code-behind: subscribe to `Lines.CollectionChanged`
in `OutputView.axaml.cs` and call `scrollViewer.ScrollToEnd()` after each
append.  This is a view-only concern and does not violate MVVM — the VM
doesn't know or care.

### 5.4 Bottom Panel — Tab Extension

The bottom panel currently shows only `ProblemsView`.  Phase 5 replaces
the bare `Grid` wrapper with a `TabControl`:

```xml
<!-- MainWindow.axaml — Grid Row 2 (bottom panel) -->
<Grid Grid.Row="2"
      IsVisible="{Binding IsBottomPanelVisible}"
      MinHeight="100"
      MaxHeight="300">
    <TabControl SelectedIndex="{Binding ActiveBottomTabIndex}">
        <TabItem Header="Problems">
            <views:ProblemsView DataContext="{Binding ProblemsViewModel}"/>
        </TabItem>
        <TabItem Header="Output">
            <views:OutputView DataContext="{Binding OutputViewModel}"/>
        </TabItem>
    </TabControl>
</Grid>
```

`ShellViewModel` gains:

```csharp
[Reactive] public int ActiveBottomTabIndex { get; set; } = 0; // 0=Problems, 1=Output
```

`ToggleProblemsCommand` continues to toggle `IsBottomPanelVisible` (no tab
switch).  A new `ToggleOutputCommand` sets `IsBottomPanelVisible = true`
and `ActiveBottomTabIndex = 1`, then on a second press with the Output tab
already active hides the panel.

The existing `ToggleTerminalCommand` / `IsTerminalVisible` stub is retired
and replaced by `ToggleOutputCommand` — the Output panel *is* the terminal
for Phase 5.  `IsTerminalVisible` is removed from `ShellViewModel`
(it was never bound to a view).

### 5.5 Key Binding — Ctrl+`

Add to `MainWindow.axaml`:

```xml
<KeyBinding Gesture="Ctrl+OemTilde" Command="{Binding ToggleOutputCommand}"/>
```

> **Note on gesture name:** Avalonia uses platform key names.
> `OemTilde` is the standard Avalonia `Key` enum value for the backtick/tilde
> key on US layouts.  Verify at M3 gate that `Ctrl+OemTilde` fires correctly
> on the development platform.  If not, fall back to
> `Ctrl+Grave` or document the limitation.

### 5.6 Menu Item

Update `MainWindow.axaml` View menu:

```xml
<MenuItem Header="Toggle _Output" Command="{Binding ToggleOutputCommand}" InputGesture="Ctrl+`"/>
```

Replace or supplement the existing `Toggle _Terminal` item.

### 5.7 Messages

No new message bus records are required for Phase 5.  `OutputViewModel`
holds a direct reference to `IProcessRunner` (injected via DI) and manages
state internally.  `FolderOpened` is the only bus message it subscribes to.

If Phase 6 or 7 needs to publish build/git output to the Output panel, a
`CommandOutput` message can be added then.  Adding it speculatively now is
YAGNI.

---

## 6. Milestones

### M1 — ProcessRunner (Service)

**Deliverables:**

- `src/Terminal/IProcessRunner.cs`
- `src/Terminal/ProcessRunner.cs`
- `CliWrap` invocation: stdout + stderr piped to `Action<string> onLine`
- `CommandResultValidation.None` (no throw on non-zero exit)
- Returns `int` exit code
- CancellationToken propagated to CliWrap; `OperationCanceledException` does
  not escape `RunAsync` — caught internally, returns `-1`

**Unit tests (no UI):**

- `RunAsync` on a known command (e.g., `echo hello`) appends the expected line
  and returns exit code 0
- `RunAsync` with a cancelled token sets `isCancelled` (output stops, no throw)
- `RunAsync` on a non-existent binary appends an error line and returns a
  non-zero code (or throws a startup exception — catch and convert)

**M1 Gate:**

| Check | Expectation |
|-------|-------------|
| `dotnet build src/aero.csproj` | 0 errors |
| `dotnet test tests` | all existing tests pass + new ProcessRunner tests |
| `ProcessRunner.RunAsync("dotnet", "--version", null, ...)` | emits a version string |

---

### M2 — OutputViewModel + OutputView ✅ DONE

**Deliverables:**

- `src/ViewModels/OutputViewModel.cs`
  - `RunCommand` / `CancelCommand` / `ClearCommand`
  - `Lines` collection; `MaxLines = 10_000`
  - `IsRunning` → drives `CanExecute` for Run/Cancel
  - `WorkingDirectory` updated from `FolderOpened`
  - UI-thread marshaling for `Lines` mutations
  - `IDisposable` implementation
- `src/Views/OutputView.axaml` + `OutputView.axaml.cs`
  - Toolbar: command text box + Run/Cancel/Clear buttons
  - ScrollViewer with `ItemsControl` displaying `Lines`
  - Auto-scroll code-behind
- Register `IProcessRunner` → `ProcessRunner` in `App.axaml.cs`
- Inject `OutputViewModel` into `ShellViewModel` constructor

**M2 Gate:**

| Check | Expectation |
|-------|-------------|
| `dotnet build src/aero.csproj` | 0 errors |
| `dotnet test tests` | all existing tests pass + new OutputViewModel tests |
| OutputView renders in isolation with dummy data | lines appear, scroll works |

---

### M3 — Bottom Panel Tabs + Ctrl+`

**Deliverables:**

- `MainWindow.axaml` — bottom panel `Grid` replaced with `TabControl` (Problems + Output tabs)
- `ShellViewModel.ActiveBottomTabIndex` reactive property
- `ToggleOutputCommand` — shows panel + switches to Output tab; hides on second press when Output active
- `ToggleTerminalCommand` retired (replaced by `ToggleOutputCommand`);
  `IsTerminalVisible` removed from `ShellViewModel`
- `Ctrl+OemTilde` key binding added
- `View → Toggle Output` menu item wired
- `View → Toggle Problems` menu item continues to toggle panel to Problems tab (or just toggle visibility)

**M3 Gate:**

| Check | Expectation |
|-------|-------------|
| `dotnet build src/aero.csproj` | 0 errors |
| `dotnet test tests` | all existing tests pass |
| Ctrl+` opens Output tab | panel appears with Output tab selected |
| Ctrl+` again closes panel | panel hides |
| View → Problems opens bottom panel to Problems tab | ProblemsView visible |
| Problems panel not broken by tab change | existing Phase 4 diagnostics still work |

---

### M4 — Integration, Hardening & Manual Test

**Deliverables:**

- End-to-end integration: enter `dotnet --version` in the command bar, press Run,
  see the version string appear in the Output panel
- Cancel button tested: run `ping -c 100 localhost` (Linux) or equivalent,
  cancel mid-run, see `[Cancelled]` line
- Exit code line appended: `[Process exited with code N]`
- Verify auto-scroll: long command output scrolls to bottom automatically
- Line cap: ensure 10 000+ line output does not grow `Lines` beyond the cap
- `Ctrl+OemTilde` gesture verified on target platform
- Update `docs/roadmap/PHASES.md` Phase 5 checklist items to `[x]`
- Update `README.md` phase status to reflect Phase 5 complete
- Create `manual_test/manual_test_phase5.sh`

**M4 Gate (Definition of Done):**

| Check | Expectation |
|-------|-------------|
| `dotnet build src/aero.csproj` | 0 errors |
| `dotnet test tests` | all tests pass |
| `manual_test_phase5.sh` | passes |
| `docs/phases/phase-5/TOFIX.md` | no open items |
| README | Phase 5 row shows ✅ |

---

## 7. File Plan

### New Files

```
src/Terminal/IProcessRunner.cs
src/Terminal/ProcessRunner.cs
src/ViewModels/OutputViewModel.cs
src/Views/OutputView.axaml
src/Views/OutputView.axaml.cs
tests/Terminal/ProcessRunnerTests.cs
tests/ViewModels/OutputViewModelTests.cs
manual_test/manual_test_phase5.sh
```

### Modified Files

```
src/MainWindow.axaml           — add Ctrl+OemTilde binding; refactor bottom panel to TabControl
src/ViewModels/ShellViewModel.cs — add OutputViewModel, ToggleOutputCommand, ActiveBottomTabIndex;
                                   retire IsTerminalVisible / ToggleTerminalCommand
src/App.axaml.cs               — register IProcessRunner → ProcessRunner; inject OutputViewModel
src/Core/Messages.cs           — no changes expected
docs/roadmap/PHASES.md         — check off Phase 5 items
docs/LIBRARIES.md              — note Pty.Net/VtNetCore are Phase 9.5, not Phase 5
README.md                      — update phase status table
```

---

## 8. Testing Strategy

### Unit Tests

**ProcessRunner tests** (`tests/Terminal/ProcessRunnerTests.cs`):

- Run a real short-lived process (e.g., `dotnet --version` or `echo`) — these
  are integration-style but do not require any installed tools beyond .NET SDK
- Cancel mid-run — verify no `OperationCanceledException` escapes `RunAsync`
- Non-zero exit code returned correctly

**OutputViewModel tests** (`tests/ViewModels/OutputViewModelTests.cs`):

- `RunCommand` cannot execute when `IsRunning == true`
- `CancelCommand` cannot execute when `IsRunning == false`
- `ClearCommand` empties `Lines`
- After `RunAsync` completes, `IsRunning` returns to `false`
- `FolderOpened` message updates `WorkingDirectory`
- Line cap: after `MaxLines` insertions, collection does not grow further
- Tests use a stub `IProcessRunner` (not a real process)

### No New UI Tests

Phase 5 introduces no new ViewModel-to-View dialog flows.  The auto-scroll
behavior lives in code-behind and is verified manually.

---

## 9. Known Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `Ctrl+OemTilde` gesture not recognized on all platforms | Medium | Verify at M3; document fallback (`Ctrl+Grave` or menu-only) |
| CliWrap `PipeTarget.ToDelegate` called on non-UI thread | Certain | UI-thread marshal in `OutputViewModel.AppendLine` |
| Large output (> 10 000 lines) slowing `ItemsControl` render | Medium | `MaxLines` cap; consider switching to `TextBox` if needed |
| `ToggleTerminalCommand` removal breaks existing subscribers | Low | `IsTerminalVisible` was never bound to a view; safe to remove |
| Auto-scroll code-behind may lag behind rapid line insertions | Low | Debounce scroll calls if needed |
| `ProcessRunner` startup exception (binary not found) | Medium | Catch `Win32Exception`/`IOException`; append error line; return -1 |

---

## 10. Phase 5 Limitations

The following are **known limitations**, not bugs, and are documented rather
than implemented:

- **No interactive input** — the Output panel is read-only; commands that
  prompt for input will hang until cancelled
- **No ANSI colors** — VT100 escape codes appear as raw text
- **Single active command** — starting a new command while one is running
  requires pressing Cancel first
- **Working directory locked to opened folder** — there is no UI to change
  the working directory per-command in Phase 5
- **No command history** — the command bar clears on each run; no up-arrow
  history

---

## 11. Definition of Done

Phase 5 is complete when **all** of the following are true:

- `dotnet build src/aero.csproj` → 0 errors
- `dotnet test tests` → all tests pass (including new Phase 5 tests)
- `manual_test/manual_test_phase5.sh` → passes
- `Ctrl+`` opens the Output panel and switches to the Output tab
- A command can be entered, run, and its stdout/stderr appears in real time
- Cancel button aborts an in-flight command; `[Cancelled]` appears
- Problems panel from Phase 4 is unaffected by the tab-control refactor
- `docs/roadmap/PHASES.md` Phase 5 items all `[x]`
- `docs/phases/phase-5/TOFIX.md` has no open items
- `README.md` updated
