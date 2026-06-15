# ISSUE-001: Dirty-close dialog never shown — tab close silently no-ops

**Label:** BUG
**Status:** open
**Priority:** critical
**Reported by:** code review (Phase 1)
**Assigned to:** —
**Related:** Phase 1 review, `EditorViewModel.PromptDirtyClose`, `Messages.cs:ConfirmDirtyClose`

## Description

Closing a tab with unsaved changes does nothing visible to the user.
The document stays open, no dialog appears, and no error is thrown.

## Steps to Reproduce

1. Open or create any file
2. Type something (document becomes dirty — tab shows `*`)
3. Press `Ctrl+W` or click the `×` on the tab

**Expected behavior:** A dialog appears asking "Save", "Don't Save", or "Cancel".
**Actual behavior:** Nothing happens. The tab stays open. The document is not closed.

## Root Cause Analysis

`EditorViewModel.PromptDirtyClose` publishes a `ConfirmDirtyClose` message to the `MessageBus`,
but no subscriber exists anywhere in the codebase.

```csharp
// EditorViewModel.cs — publishes into the void
_bus.Publish(new ConfirmDirtyClose(fileName, HandleResponse));
```

```bash
# Confirmed: only definition + one publish site, zero subscribers
grep -rn "ConfirmDirtyClose" src/
# → src/Core/Messages.cs        (record definition)
# → src/ViewModels/EditorViewModel.cs  (Publish call only)
```

The `HandleResponse` callback is constructed correctly and would work if called,
but it is never invoked because no one receives the message.

## Notes

- `DirtyCloseResponse` constants (`Save`, `DontSave`, `Cancel`) are defined and correct.
- The save-then-close async path (`SaveAndCloseAsync`) is correctly implemented.
- The only missing piece is a UI subscriber that shows the dialog and calls `OnResponse`.
- Subscriber should live in `MainWindow.axaml.cs` (or a dedicated dialog service) —
  not in a ViewModel — because it needs to open an Avalonia `Window`/dialog.
- Consider using `DialogHost.Avalonia` (already catalogued in `docs/LIBRARIES.md`) or
  Avalonia's built-in `MessageBox` equivalent.

## Debug Log

> Not yet attempted. Root cause is confirmed statically.

## Resolution

- **Root cause:** —
- **Fix:** —
- **Commit:** —
- **Closed date:** —
