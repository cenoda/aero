# ISSUE-004: Untitled "Save" on dirty close silently discards content

- **Label:** BUG
- **Priority:** critical
- **Status:** closed
- **Opened:** 2026-06-17
- **Closed:** 2026-06-17

## Description

When closing a dirty untitled document (not yet saved to a file), choosing
**Save** in the dirty-close dialog silently discards the content instead of
saving it, because both callers of `SaveDocumentAsync` ignore its `false`
return value for new/untitled documents.

## Reproduction

1. Open Aero IDE
2. Type some content in the untitled tab
3. Close the tab (Ctrl+W)
4. Click **Save** in the dirty-close dialog
5. **Expected:** File is saved (via Save As dialog) or tab stays open
6. **Actual:** Tab closes, content is lost

## Root Cause

`DocumentManager.SaveDocumentAsync` returns `false` when `doc.IsNew` (line 116–119
of DocumentManager.cs). Two callers ignore this return:

1. **`EditorViewModel.SaveAndCloseAsync`** (line 207–208): ignores the `false` return
   and calls `onClose()` unconditionally, closing the tab and losing content.

2. **`ShellViewModel.ExitAsync`** (the old code at line 228): same pattern — ignores
   `false` return, proceeds with exit.

## Resolution

### `EditorViewModel.SaveAndCloseAsync`
Check the `bool` return from `SaveDocumentAsync`. If `false`, surface a status bar
message ("Untitled file — use Save As (Ctrl+Shift+S) first") and *do not* close the tab.

### `ShellViewModel.ExitAsync` → `CheckDirtyBeforeExitAsync`
Refactored exit dirty-check into `CheckDirtyBeforeExitAsync()` (reused by
MainWindow.OnClosing for ISSUE-005). When `SaveDocumentAsync` returns `false`,
show a Save As file picker dialog. If the user cancels Save As, cancel the exit.

### Additional: `CloseActiveTabImpl` / `CloseTabImpl` deduplication
These were byte-for-byte identical. Collapsed into a single `CloseTabImpl` method.

## Files Changed

- `src/ViewModels/EditorViewModel.cs` — fix `SaveAndCloseAsync`, deduplicate impl
- `src/ViewModels/ShellViewModel.cs` — extract `CheckDirtyBeforeExitAsync`, add `SaveAsDialogForDocAsync`

## Related

- ISSUE-001: marked the untitled Save-as path "out of scope" — now fixed
- ISSUE-005: companion fix for the window close ("X") path
