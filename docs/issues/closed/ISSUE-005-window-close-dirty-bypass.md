# ISSUE-005: OS window close ("X") bypasses dirty-check prompt

- **Label:** BUG
- **Priority:** high
- **Status:** closed
- **Opened:** 2026-06-17
- **Closed:** 2026-06-17

## Description

Clicking the OS window chrome close button ("X") bypasses the dirty-document
check entirely. `MainWindow.OnClosing` only unsubscribed from the MessageBus
— it never inspected dirty documents or set `e.Cancel`. The Save/Don't Save/Cancel
flow only ran through **File → Exit** (`ExitCommand`).

## Reproduction

1. Open Aero IDE
2. Type some content in an untitled (or modified) tab
3. Click the window "X" button (OS chrome, top right)
4. **Expected:** Dirty-close dialog appears (Save / Don't Save / Cancel)
5. **Actual:** Window closes immediately, content is lost

## Root Cause

`MainWindow.OnClosing` (old code, lines 32–39):
```csharp
private void OnClosing(object? sender, WindowClosingEventArgs e)
{
    if (_bus != null && _confirmDirtyCloseHandler != null)
    {
        _bus.Unsubscribe<ConfirmDirtyClose>(_confirmDirtyCloseHandler);
        _confirmDirtyCloseHandler = null;
    }
}
```
No dirty check, no `e.Cancel`, no prompt.

By contrast, `ShellViewModel.ExitAsync` properly iterated dirty documents
and published `ConfirmDirtyClose` messages with await.

## Resolution

1. **Extracted `CheckDirtyBeforeExitAsync()`** in `ShellViewModel` — shared by
   both File→Exit and OS window close paths.

2. **`MainWindow.OnClosing`** now:
   - If `_exitHandled` is true (set by `ExitAsync` → `MarkExitHandled()`),
     just cleans up the bus and lets close proceed.
   - Otherwise, cancels the close (`e.Cancel = true`), calls
     `shell.CheckDirtyBeforeExitAsync()`, and if confirmed, sets `_exitHandled`
     and calls `Close()` which re-enters OnClosing cleanly.

3. **`ExitAsync`** now calls `mw.MarkExitHandled()` before `desktop.Shutdown()`
   to prevent the double-check when OnClosing fires again.

## Files Changed

- `src/MainWindow.axaml.cs` — add `_exitHandled` flag, rewrite `OnClosing`,
  add `MarkExitHandled()`, extract `UnsubscribeBus()`
- `src/ViewModels/ShellViewModel.cs` — extract `CheckDirtyBeforeExitAsync()`,
  call `mw.MarkExitHandled()` before shutdown

## Related

- ISSUE-004: companion fix for untitled Save data loss during close/exit
- ISSUE-001: original dirty-close dialog implementation (tab-close path)
