# Phase 6 — Implementation Plan

> **Phase:** 6 — Build & Output (Abstraction-First)
> **Date:** 2026-06-21
> **Status:** Ready for implementation

---

## 1. Goal

Let the user build the current workspace from inside Aero, watch the build
stream into the Output panel, see the resulting errors/warnings in the Problems
panel, and click a problem to jump to the offending file and line.

Key deliverables:

- `IBuildService` — abstraction over a build system (interface-first, per `AGENTS.md` §4)
- `DotNetBuildService` — the **only** concrete implementation this phase (`.sln` / `.csproj`)
- `BuildServiceFactory` — auto-detects the workspace build system (reuses `IProjectLoader`)
- `BuildOptions` / `BuildResult` / `ParsedError` models
- `Ctrl+Shift+B` runs the detected build; output streams into the existing Output tab
- Build errors/warnings populate the existing **Problems** panel (alongside LSP diagnostics)
- Clicking a problem opens the file and moves the caret to the line/column
- Build state is surfaced in the status bar (`Building… / Build succeeded / Build failed`)

This phase is intentionally **basic**: one build system (.NET), one active build
at a time, full-output parse at completion. Incremental/streaming error parsing,
multi-target selection, custom build args UI, and non-.NET build systems are out
of scope (see §10 Limitations).

---

## 2. Entry Check (verified against live code 2026-06-21)

Phase 5 is complete and the gate holds:

| Gate | Verification | Result |
|------|--------------|--------|
| Phase 5 checklist complete | `docs/roadmap/PHASES.md` Phase 5 items all `[x]` | ✅ |
| Phase 5 TOFIX empty | `docs/phases/phase-5/TOFIX.md` no open items | ✅ |
| Build passes | `dotnet build src/aero.csproj` | ✅ 0 errors |
| Tests pass | `dotnet test tests` | ✅ **302/302** |
| .NET SDK present | `dotnet --version` | ✅ **9.0.117** |

### Seams Phase 6 builds on (each verified in `src/`)

- **`Aero.Terminal.IProcessRunner`** (`src/Terminal/IProcessRunner.cs`):
  `Task<int> RunAsync(string executable, string arguments, string? workingDirectory, Action<string> onLine, CancellationToken)`.
  `ProcessRunner` catches **all** exceptions and returns `-1` (never throws except it
  also swallows `OperationCanceledException` → `-1`). Reusable as-is for build.
- **`OutputViewModel`** (`src/ViewModels/OutputViewModel.cs`): owns `Lines`
  (`ObservableCollection<string>`), UI-thread marshaling, `MaxLines` cap, `IsRunning`,
  cancel + exit-line constants, `WorkingDirectory` (kept current via `FolderOpened`).
  **Today it can only run from its `CommandText` bar — there is no public API to stream
  externally-produced output through it.** (Gap addressed in M3.)
- **`DiagnosticStore`** (`src/Languages/DiagnosticStore.cs`): keyed by **`fileUri` only**,
  documented "one writer: LSPManager", `SetDiagnostics(uri, list)` **replaces** all
  diagnostics for that uri and publishes `DiagnosticsUpdated(all)`. **Build must not
  clobber LSP diagnostics for the same file** (design decision in §5.4).
- **`Diagnostic`** record (`src/Languages/Models/Diagnostic.cs`):
  `(DiagnosticSeverity, FileUri, TextRange, Message, Source?, Code?)`. `TextRange` is
  **0-based**; `LocationText`/display add `+1`. Has a `Source` field already — useful as the
  build/LSP discriminator.
- **`ProblemsViewModel`** (`src/ViewModels/ProblemsViewModel.cs`): subscribes to
  `DiagnosticsUpdated`, rebuilds `Diagnostics` ordered by `FileUri` then `StartLine`.
  **Read-only — no click/navigation today.** (Navigation added in M4.)
- **`IProjectLoader`** (`src/Services/ProjectLoader.cs`): `DetectProjectKind(path)` and
  `DetectProjects(root)` already classify `.sln` → `Solution`, `.csproj` → `CSharpProject`,
  `package.json` → `NodeProject`. The factory reuses this — no new detection logic.
- **`src/Core/Messages.cs`** already declares `BuildStarted(string Project)` and
  `BuildFinished(int ExitCode, string Output)` (pre-positioned, currently unused). There is
  **no** navigation message yet.
- **`MainWindow.axaml`**: key bindings live in `<Window.KeyBindings>`; the status-bar
  `Grid` is `ColumnDefinitions="Auto,Auto,*,Auto,Auto"` — **column 2 (`*`) is free** for a
  build-status label. `Ctrl+OemTilde` → `ToggleOutputCommand` already exists.
- **`EditorViewModel.OpenFileAsync(path)`** opens/activates a tab. Caret control lives in
  `EditorView.axaml.cs` against the live `TextEditor`; the `FindReplaceRequested` event is the
  established **VM-event → code-behind-acts-on-control** precedent to copy for navigation.
- **DI** (`src/App.axaml.cs`): all services are singletons; `LSPManager` is **eagerly
  resolved** so its `FolderOpened` subscription and disposal run. Any Phase 6 coordinator that
  subscribes to messages must follow the same eager-resolve pattern.

---

## M0 — Entry Gate

Already satisfied (see §2). Re-verify immediately before coding:

```bash
dotnet build src/aero.csproj    # 0 errors
dotnet test tests               # 302/302 green
```

---

## 3. Scope

### In Scope

1. `IBuildService` interface + `BuildOptions` / `BuildResult` / `ParsedError` models
2. `DotNetBuildService` — runs `dotnet build <target>` via `IProcessRunner`, parses MSBuild diagnostics
3. `BuildServiceFactory` — detects the workspace build system from `IProjectLoader`
4. `ShellViewModel.BuildCommand` + `Ctrl+Shift+B` + `Build` menu item
5. Build output streamed into the existing **Output** tab (reuses `OutputViewModel`)
6. `BuildStarted` / `BuildFinished` published; status-bar build state
7. Build diagnostics surfaced in the **Problems** panel without clobbering LSP diagnostics
8. Click a problem → open file + move caret to line/column
9. `manual_test/manual_test_phase6.sh`

### Out of Scope (see §10)

- Non-.NET build systems (`Npm`/`Cargo`/`Make`) — interface + factory are ready, but only
  `DotNetBuildService` is implemented (YAGNI, per `plan-rules` §3)
- Incremental/streaming error parsing (parse the full captured output once at completion)
- Build configuration UI (Debug/Release, target framework picker, custom args)
- Concurrent builds / build queue (single active build, mirrors Output panel's single command)
- Restore/clean/run/test commands (build only)
- Caching, incremental MSBuild graph awareness beyond what `dotnet build` already does

---

## 4. Dependency Decision

**No new NuGet packages.** `dotnet build` is invoked through the existing
`CliWrap`-backed `IProcessRunner` (csproj already references `CliWrap 3.*`).
The .NET SDK (`9.0.117`) is an **entry prerequisite** — `DotNetBuildService`
shells out to the `dotnet` binary on `PATH`. If `dotnet` is missing, the build
must fail gracefully (the existing `ProcessRunner` "binary not found → `-1` +
`[Error: …]` line" path handles this).

> **Deviation from `docs/phases/phase-6/README.md` (recorded per `plan-rules` §1):**
> the README sketch gives `IBuildService` an `IAsyncEnumerable<string> StreamOutputAsync`
> *and* a `BuildAsync`. That duplicates streaming we already get from `IProcessRunner`.
> The real interface (§5.1) folds streaming into `BuildAsync(... Action<string> onLine ...)`
> and drops `StreamOutputAsync`. The README is aspirational; the live `IProcessRunner`
> contract wins.

---

## 5. Architecture

### 5.1 Build abstraction (`src/Services/Build/`)

```csharp
// src/Services/Build/IBuildService.cs
namespace Aero.Services.Build;

public interface IBuildService
{
    /// <summary>Human-readable id, e.g. "dotnet".</summary>
    string Name { get; }

    /// <summary>
    /// Build the target described by <paramref name="options"/>, streaming each
    /// stdout/stderr line to <paramref name="onLine"/>. Never throws for build
    /// failures or a missing toolchain — failures are reported via the result.
    /// </summary>
    Task<BuildResult> BuildAsync(
        BuildOptions options,
        Action<string> onLine,
        CancellationToken cancellationToken);

    /// <summary>Parse captured build output into structured errors/warnings.</summary>
    IReadOnlyList<ParsedError> ParseErrors(IReadOnlyList<string> outputLines);
}
```

```csharp
// src/Services/Build/BuildModels.cs (small related records — allowed by CONVENTIONS)
public record BuildOptions(
    string WorkingDirectory,
    string? TargetPath = null,     // specific .sln/.csproj; null = let dotnet pick
    bool IsCleanBuild = false);

public record BuildResult(
    bool Success,
    int ExitCode,
    TimeSpan Duration,
    IReadOnlyList<ParsedError> Errors);

public enum BuildSeverity { Error, Warning }

public record ParsedError(
    string FilePath,   // absolute path as emitted by MSBuild
    int Line,          // 1-based (MSBuild convention)
    int Column,        // 1-based
    string Code,       // e.g. "CS0029"
    string Message,
    BuildSeverity Severity);
```

### 5.2 `DotNetBuildService`

Location: `src/Services/Build/DotNetBuildService.cs`.

- `Name => "dotnet"`.
- `BuildAsync`: builds the argument string
  `build [TargetPath] [-c …] [/clp:NoSummary]` (clean build → `clean` first or
  `--no-incremental`; for Phase 6 just pass `--no-incremental` when `IsCleanBuild`).
  Captures all lines (also tees them to `onLine` for the Output panel), times the run,
  then `ParseErrors` over the captured lines. Delegates process execution to the injected
  `IProcessRunner` — **no direct `Process`/`CliWrap` use here.**
- `ParseErrors`: regex over each line. **Format confirmed against `dotnet build` on this
  machine:**

  ```
  /abs/path/Program.cs(5,17): error CS0029: Cannot implicitly convert ... [/abs/path/probe.csproj]
  /abs/path/File.cs(7,13): warning CS0168: The variable 'y' is declared but never used [project]
  ```

  Proposed pattern (Rust/.NET regex):

  ```
  ^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+):\s+(?<msg>.+?)(\s+\[[^\]]+\])?$
  ```

  Notes baked into tests: the trailing ` [project]` is stripped; lines without a
  `file(line,col)` prefix (e.g. general `MSBxxxx` toolchain errors) are ignored in
  Phase 6 (documented limitation), MSBuild line/col are **1-based**.

### 5.3 `BuildServiceFactory`

Location: `src/Services/Build/BuildServiceFactory.cs`.

```csharp
public IBuildService? Detect(string workspacePath);
```

- Reuses `IProjectLoader.DetectProjects(workspacePath)`.
- If any `ProjectKind.Solution` or `ProjectKind.CSharpProject` is found → return the
  `DotNetBuildService` (prefer `.sln` over `.csproj` when both exist; pick the first `.sln`).
- Otherwise → `null` (no supported build system; the command shows a status message).
- `NodeProject` is recognized by the loader but **not built** in Phase 6 (no `NpmBuildService`).

### 5.4 Diagnostics: build + LSP coexistence (the key correctness decision)

`DiagnosticStore` is currently single-writer and keyed by `fileUri`. If build wrote
`SetDiagnostics(uri, …)` it would **erase** LSP diagnostics for that file (and vice-versa).
This is the "real second writer" the Phase 4 plan said to wait for before generalizing.

**Decision:** add a `source` dimension to `DiagnosticStore` so two writers coexist:

```csharp
void SetDiagnostics(string source, string fileUri, IReadOnlyList<Diagnostic> diagnostics);
void ClearDiagnostics(string source, string fileUri);
void ClearSource(string source);   // wipe all build diagnostics before a fresh build
```

- Internal key becomes `(source, fileUri)`; `GetAllDiagnostics` merges across sources.
- `LSPManager` calls become `source: "lsp"`; `DotNetBuildService` results are written under
  `source: "build"`.
- Before each build, call `ClearSource("build")` so stale build errors don't accumulate.
- `ProblemsViewModel` is **unchanged** — it still consumes the merged `DiagnosticsUpdated` list.
- This is a focused change to one Phase 4 file + its tests; **no `IDiagnosticStore`
  interface is introduced** (still no third writer — YAGNI).

Mapping `ParsedError` → `Diagnostic`: `FileUri = new Uri(Path.GetFullPath(FilePath)).AbsoluteUri`
(matches `LSPManager`/`EditorView` URI format), `TextRange(Line-1, Column-1, Line-1, Column-1)`,
`Severity` Error/Warning, `Source = "build"`, `Code = code`.

### 5.5 Running the build through the Output panel

`OutputViewModel` needs a public entry point so the build coordinator can reuse its
streaming/cancel/exit-line/line-cap machinery instead of duplicating it:

```csharp
// OutputViewModel — extract a shared private core from the existing RunAsync,
// then expose:
public Task RunExternalAsync(string executable, string arguments, string? workingDir, CancellationToken ct);
```

- Reuses the existing `IsRunning`, `_wasCancelled`, `AppendLine`, exit-line constants.
- Honors the **single-active-command** rule: if `IsRunning`, the build command no-ops with a
  status message (consistent with the command bar's `canRun`).
- The build coordinator shows the panel + selects the Output tab the same way `ToggleOutput`
  does (`IsBottomPanelVisible = true; ActiveBottomTabIndex = 1`).

### 5.6 Build coordination + command wiring

A small coordinator owns the build flow. Two options:

- **(A) `BuildCoordinator` service** (subscribes to nothing; invoked by the command), or
- **(B) fold into `ShellViewModel.BuildCommand`** calling factory + service directly.

**Choice: (B) a thin `ShellViewModel.BuildCommand`** that delegates to an injected
`BuildServiceFactory` + `IProcessRunner` + `DiagnosticStore` + `OutputViewModel`. No new
message subscriber → no eager-resolve concern. (If Phase 7 Git wants the same orchestration,
extract `BuildCoordinator` then — YAGNI.)

`BuildCommand` flow:

1. Resolve workspace root (the last `FolderOpened` path; if none → status "Open a folder to build").
2. `factory.Detect(root)`; if `null` → status "No supported build system found".
3. Publish `BuildStarted(targetPath)`, set status `Building…`, show Output tab.
4. `DiagnosticStore.ClearSource("build")`.
5. Run via `OutputViewModel.RunExternalAsync("dotnet", args, root, ct)` **while** capturing
   lines for parsing (the coordinator passes its own `onLine` that both appends and buffers —
   so the service's `BuildAsync` returns the parsed `BuildResult`).
6. On completion: map `result.Errors` → `Diagnostic`s grouped by file, write each file's set
   via `DiagnosticStore.SetDiagnostics("build", uri, list)`.
7. Publish `BuildFinished(exitCode, "")` (output already streamed; keep `Output` arg empty or a
   short summary), set status `Build succeeded`/`Build failed (N errors)`.

`Ctrl+Shift+B` binding in `MainWindow.axaml` + a top-level `Build` menu (`Build`/`Rebuild`).

### 5.7 Click problem → navigate (new seam)

- `ProblemsViewModel` gains a `NavigateCommand` (parameter: selected `Diagnostic`) bound to
  double-click / Enter on the `ListBox` item.
- Navigation message added to `Messages.cs`:
  `public record NavigateToLocation(string FilePath, int Line, int Column);` (0-based to match
  `TextRange`, or 1-based — pick one and document; recommend **0-based** to match `Diagnostic.Range`).
- `EditorViewModel` subscribes: on `NavigateToLocation`, `await OpenFileAsync(path)` to
  ensure/activate the tab, then raise a `NavigationRequested` event (mirroring
  `FindReplaceRequested`) that `EditorView.axaml.cs` handles to set caret offset + `ScrollTo`
  on the live `TextEditor`. Caret is set after the existing `Loaded`-priority resubscribe
  binds the editor (best-effort; see risk R6.4).

### 5.8 DI registration (`src/App.axaml.cs`)

```csharp
// Phase 6 — Build
services.AddSingleton<IBuildService, DotNetBuildService>();
services.AddSingleton<BuildServiceFactory>();
```

`DotNetBuildService` takes `IProcessRunner` via constructor. `BuildServiceFactory` takes
`IProjectLoader` + the `IBuildService`(s). `ShellViewModel` gains `BuildServiceFactory` +
`IProcessRunner` + `DiagnosticStore` constructor params (all already singletons).

---

## 6. Milestones

### M1 — Build abstraction + `DotNetBuildService` (no UI)

Deliverables: `IBuildService`, `BuildOptions`/`BuildResult`/`ParsedError`/`BuildSeverity`,
`DotNetBuildService` (`BuildAsync` via injected `IProcessRunner`, `ParseErrors`).

Tests (`tests/Services/Build/`):
- `ParseErrors` against the **real captured** MSBuild lines (error, warning, trailing `[project]`).
- `ParseErrors` ignores non-diagnostic lines.
- `BuildAsync` with a stub `IProcessRunner` returns `Success=false` when exit code ≠ 0 and
  surfaces parsed errors; `Success=true`/empty errors on exit 0.

Gate: `dotnet build src/aero.csproj` 0 errors; new tests pass; existing 302 still green.

### M2 — `BuildServiceFactory` + DI

Deliverables: `BuildServiceFactory.Detect` (reuses `IProjectLoader`); DI registration.

Tests: temp dir with `.csproj` → returns dotnet service; with `.sln` → dotnet (prefers sln);
empty dir / only `package.json` → `null`.

Gate: build + tests green.

### M3 — Build command, Output streaming, status bar

Deliverables: `OutputViewModel.RunExternalAsync` (extract shared core); `ShellViewModel.BuildCommand`;
`Ctrl+Shift+B` + `Build` menu; `BuildStarted`/`BuildFinished` published; status-bar build state
(status column 2). Show + select Output tab on build.

Tests: `BuildCommand` with no folder → status message, no run; with stub factory returning a stub
service → `IsRunning` transitions, `BuildStarted`/`BuildFinished` published, status text updates.

Gate: build + tests green; manual: `Ctrl+Shift+B` on this repo streams `dotnet build` output into
the Output tab.

### M4 — Problems integration + click-to-navigate

Deliverables: `DiagnosticStore` source dimension (+ `LSPManager` updated to `source:"lsp"`);
`ParsedError`→`Diagnostic` mapping written under `source:"build"` with `ClearSource("build")`
before each build; `ProblemsViewModel.NavigateCommand`; `NavigateToLocation` message;
`EditorViewModel` navigation handling + `EditorView` caret/scroll.

Tests: `DiagnosticStore` — build and LSP diagnostics for the **same file** coexist and don't
clobber; `ClearSource` removes only build entries; merged ordering stable. `ProblemsViewModel`
publishes `NavigateToLocation` on navigate.

Gate: build + tests green; manual: introduce a CS error in this repo, build, see it in Problems,
click → editor opens at the line.

### M5 — Integration, hardening, docs

Deliverables: end-to-end manual run on this repo (error present → fix → rebuild → Problems clears);
`dotnet` missing path shows a graceful error line; `manual_test/manual_test_phase6.sh`;
`docs/roadmap/PHASES.md` Phase 6 items `[x]`; `README.md` phase status; `docs/LIBRARIES.md` note
(no new deps; build uses CliWrap via IProcessRunner); resolve `docs/phases/phase-6/TOFIX.md`.

Gate (Definition of Done): see §11.

---

## 7. File Plan

### New
```
src/Services/Build/IBuildService.cs
src/Services/Build/BuildModels.cs            (BuildOptions/BuildResult/ParsedError/BuildSeverity)
src/Services/Build/DotNetBuildService.cs
src/Services/Build/BuildServiceFactory.cs
tests/Services/Build/DotNetBuildServiceTests.cs
tests/Services/Build/BuildServiceFactoryTests.cs
manual_test/manual_test_phase6.sh
docs/phases/phase-6/TOFIX.md                 (created with this plan)
```

### Modified
```
src/Languages/DiagnosticStore.cs   — add source dimension (+ ClearSource)
src/Languages/LSPManager.cs        — pass source:"lsp" to Set/ClearDiagnostics
src/ViewModels/OutputViewModel.cs  — extract core + RunExternalAsync
src/ViewModels/ShellViewModel.cs   — BuildCommand, status-bar build state, ctor deps
src/ViewModels/ProblemsViewModel.cs— NavigateCommand
src/ViewModels/EditorViewModel.cs  — NavigateToLocation handling + NavigationRequested event
src/Views/EditorView.axaml.cs      — caret/scroll on navigation
src/Views/ProblemsView.axaml       — double-click/Enter → NavigateCommand
src/Core/Messages.cs               — add NavigateToLocation (BuildStarted/BuildFinished already exist)
src/MainWindow.axaml               — Ctrl+Shift+B binding + Build menu + status build label
src/App.axaml.cs                   — register IBuildService + BuildServiceFactory; ShellViewModel deps
docs/roadmap/PHASES.md             — check off Phase 6
docs/LIBRARIES.md / README.md      — phase status notes
tests/Languages/DiagnosticStoreTests.cs / LSPManagerTests.cs — source-dimension updates
```

---

## 8. Testing Strategy

- **Unit (no UI):** `ParseErrors` (real format), factory detection, `DiagnosticStore`
  multi-source coexistence, `BuildAsync` over a stub `IProcessRunner`.
- **ViewModel:** `BuildCommand` state transitions + messages with a stubbed factory/service
  (no real `dotnet` in unit tests); `ProblemsViewModel.NavigateCommand`.
- **Integration-style:** `DotNetBuildService` against a tiny temp project that intentionally
  fails to compile — asserts the parsed error count/codes. Requires the .NET SDK (entry
  prerequisite), mirrors Phase 5's `ProcessRunner` real-process tests.
- **Manual:** `manual_test_phase6.sh` — build this repo, inject an error, rebuild, click a
  problem, fix, rebuild clears it.

---

## 9. Risks

| Id | Risk | Likelihood | Mitigation |
|----|------|-----------|------------|
| R6.1 | Build diagnostics clobber LSP diagnostics for the same file | Certain (without fix) | `DiagnosticStore` source dimension (§5.4); test coexistence |
| R6.2 | MSBuild localized output breaks the English `error/warning` regex | Medium | Force invariant: pass `dotnet build` with `--property:UseSharedCompilation=false`? No — instead set env/`-clp` and document English-locale assumption as a limitation; regex anchors on `CSxxxx` code shape |
| R6.3 | `dotnet` not on PATH | Medium | `ProcessRunner` already returns `-1` + `[Error: …]`; surface "Build failed: dotnet not found" in status |
| R6.4 | Caret navigation races the `Loaded`-priority editor resubscribe | Medium | Set caret after `OpenFileAsync` completes + post at `Loaded` priority like the existing resubscribe; best-effort, documented |
| R6.5 | Long build output exceeds `OutputViewModel.MaxLines` (10k) and trims early lines | Low | Parsing uses the coordinator's own captured buffer, **not** the trimmed `Lines` collection |
| R6.6 | Build run conflicts with an in-flight command-bar command (single `IsRunning`) | Low | `BuildCommand` no-ops with a status message when `IsRunning` |
| R6.7 | `BuildFinished.Output` could duplicate megabytes already streamed | Low | Pass empty/short summary in `BuildFinished`; output lives in the Output panel |

---

## 10. Phase 6 Limitations (by design)

- **.NET only.** `IBuildService` + factory are extensible, but only `DotNetBuildService` ships.
  Npm/Cargo/Make are future phases (do not re-add speculatively — `plan-rules` §7).
- **Single active build**, no queue (shares the Output panel's single-command model).
- **Whole-output parse at completion**, not incremental; no live error count during the build.
- **English-locale MSBuild output** assumed for parsing; localized SDKs may not parse.
- **Toolchain-level errors** (`MSBxxxx` lines without a `file(line,col)` prefix) are streamed to
  Output but **not** added to Problems.
- **No build configuration UI** (Debug/Release, TFM, custom args) — default `dotnet build`.
- **Navigation is best-effort** on the just-opened tab; rapid re-clicks may need a second click.

---

## 11. Definition of Done

- `dotnet build src/aero.csproj` → 0 errors
- `dotnet test tests` → all pass (302 existing + new Phase 6 tests)
- `Ctrl+Shift+B` builds the detected workspace; output streams into the Output tab
- Build errors/warnings appear in the Problems panel **without** erasing LSP diagnostics
- Clicking a problem opens the file and moves the caret to the line/column
- Status bar shows `Building… / Build succeeded / Build failed (N)`
- `BuildStarted` / `BuildFinished` are published
- `manual_test/manual_test_phase6.sh` passes
- `docs/roadmap/PHASES.md` Phase 6 items all `[x]`; `README.md` updated
- `docs/phases/phase-6/TOFIX.md` has no open items
