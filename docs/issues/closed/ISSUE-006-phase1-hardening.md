# ISSUE-006: Phase 1 robustness hardening before Phase 2

- **Label:** BUG
- **Priority:** medium
- **Status:** closed
- **Opened:** 2026-06-17
- **Closed:** 2026-06-17

## Description

Two robustness gaps found during a pre-Phase-2 audit. Neither is a regression;
both are latent defects that become much more likely once Phase 2 (file explorer,
Open Folder, click-to-open, FileSystemWatcher) starts exercising file I/O heavily.

### 6a — Unhandled file-I/O exceptions can crash the app

`OpenFileCommand`, `SaveCommand`, and `SaveAsCommand` are built with
`ReactiveCommand.CreateFromTask`. When the underlying `File.ReadAllTextAsync` /
`File.WriteAllTextAsync` throws (file deleted mid-open, permission denied, disk
full, locked/binary file), the exception flows to `ReactiveCommand.ThrownExceptions`.
Nothing subscribes to that observable anywhere in the codebase, so ReactiveUI's
default handler rethrows on the UI scheduler — crashing the application.

### 6b — Replace All is not a single undo unit

`EditorView.ExecuteReplaceAll` issued one `Document.Replace` per match, so each
replacement became a separate undo entry. Reverting a Replace All required pressing
Ctrl+Z once per match instead of once for the whole operation (every mainstream
editor treats Replace All as a single undo step).

## Resolution

**6a** — Wrapped the file-I/O calls in `try/catch` inside `ShellViewModel`:
- `OpenFileAsync` — catches read failures, reports via `StatusText`.
- `SaveAsync` — catches save failures, reports via `StatusText`.
- `SaveAsWithDialogAsync` — catches Save As write failures.
- `SaveAsDialogForDocAsync` (exit path) — on write failure, reports and returns
  `false` so the exit is cancelled rather than discarding unsaved work.

**6b** — Wrapped the reverse-order replace loop in
`Document.BeginUpdate()` / `EndUpdate()` (try/finally) so the entire Replace All
collapses into one undo group. Also added an early-out when there are no matches.

## Files Changed

- `src/ViewModels/ShellViewModel.cs` — guard Open/Save/SaveAs I/O with try/catch
- `src/Views/EditorView.axaml.cs` — group Replace All into a single undo unit

## Verification

- `dotnet build` — clean (0 warnings, 0 errors)
- `dotnet test` — 89/89 passing

## Related

- ISSUE-004 / ISSUE-005: companion data-loss fixes on the dirty-close/exit paths
