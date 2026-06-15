# ISSUE-003: Phase 1 minor issues — dead state, keyboard shortcuts, message semantics, leaks

**Label:** BUG
**Status:** open
**Priority:** low
**Reported by:** code review (Phase 1)
**Assigned to:** —
**Related:** Phase 1 review, ISSUE-001, ISSUE-002

## Description

A collection of low-priority issues identified during Phase 1 review.
None block Phase 2 entry, but should be resolved before Phase 8 (UI Polish).

---

### 3a — `FindReplaceViewModel.IsVisible` is dead state

`FindReplaceViewModel` has `[Reactive] public bool IsVisible`, `Show()`, and `Hide()` methods
that are never used. Actual overlay visibility is driven by `EditorViewModel.IsFindReplaceVisible`.

**Risk:** Future code might accidentally bind to `FindReplaceViewModel.IsVisible` (always `false`)
instead of `EditorViewModel.IsFindReplaceVisible`, causing the overlay to disappear.

**Fix:** Remove `IsVisible`, `Show()`, and `Hide()` from `FindReplaceViewModel`, or wire them up
so `FindReplaceViewModel.IsVisible` is the single source of truth and `IsFindReplaceVisible` is removed.

---

### 3b — Replace and Find commands are identical

`ShellViewModel.Find()` and `Replace()` both call `_editorViewModel.ShowFindReplace()`.
`Ctrl+H` and `Ctrl+F` produce the same result — the overlay opens in the same state.

**Fix:** Add a parameter or separate entry point so `Ctrl+H` expands the Replace row
or focuses the Replace field on open. E.g., `ShowFindReplace(focusReplace: true)`.

---

### 3c — `Ctrl+F` / `Ctrl+H` don't fire when focus is in the TextEditor

`InputGesture` on menu items only intercepts keys when focus is within the menu.
`AvaloniaEdit.TextEditor` consumes `KeyDown` events first.
`MainWindow.OnKeyDown` is registered but the handler body is empty.

**Affected shortcuts:** `Ctrl+F`, `Ctrl+H` (and potentially `Ctrl+Z`, `Ctrl+Y` in some focus states).

**Fix:** In `MainWindow.OnKeyDown`, handle `Ctrl+F` → `ShellViewModel.FindCommand.Execute()`
and `Ctrl+H` → `ShellViewModel.ReplaceCommand.Execute()`. Or use Avalonia's `KeyBindings`
collection on the `Window` element in XAML, which has higher priority than child controls.

---

### 3d — `DocumentOpened` message misuses `FilePath` field for untitled documents

```csharp
// DocumentManager.NewDocument()
_bus.Publish(new DocumentOpened(doc.DisplayName));  // "Untitled", "Untitled-2", etc.
```

The `DocumentOpened` record parameter is named `FilePath`, implying a real path.
Passing a display name violates the contract and will mislead future subscribers
(e.g. the file tree in Phase 2 that may attempt to `File.Exists(msg.FilePath)`).

**Fix options:**
- Add a nullable `FilePath` + a separate `DisplayName` field to `DocumentOpened`.
- Or: don't publish `DocumentOpened` for new untitled documents at all
  (it is currently used only as a safety net in `EditorViewModel.OnDocumentOpened`,
  which already guards with a `FilePath != null` check).

---

### 3e — `ShellViewModel` never unsubscribes from `MessageBus`

`ShellViewModel` subscribes to three messages in its constructor and never unsubscribes.
As a `Singleton` this is harmless in the current app lifetime, but it violates the
`IDisposable` pattern established by `EditorTabViewModel` and will cause stale-handler
bugs if `ShellViewModel` is ever re-created (e.g. in tests or future multi-window support).

**Fix:** Implement `IDisposable` on `ShellViewModel` and unsubscribe in `Dispose()`,
matching the pattern in `EditorTabViewModel`.

---

## Resolution

- **Root cause:** Multiple minor design issues from Phase 1 code review:
  - 3a: `FindReplaceViewModel` had dead `IsVisible`/`Show()`/`Hide()` never wired
  - 3b: `ShellViewModel.Find()` and `Replace()` both called identical `ShowFindReplace()`
  - 3c: `Ctrl+F`/`Ctrl+H` shortcuts only worked when menu had focus (AvaloniaEdit consumes KeyDown first)
  - 3d: `DocumentManager.NewDocument()` published `DocumentOpened` with display name in `FilePath` field
  - 3e: `ShellViewModel` never unsubscribed from `MessageBus`
- **Fix:**
  - 3a: Removed `IsVisible` property and `Show()`/`Hide()` methods from `FindReplaceViewModel`
  - 3b: Added `FocusReplaceOnOpen` property to `FindReplaceViewModel`; `ShowFindReplace(bool focusReplace)` sets it; `FindReplaceOverlay` code-behind focuses Replace TextBox; `ShellViewModel.Replace()` passes `focusReplace: true`
  - 3c: `MainWindow.OnKeyDown` handles `Ctrl+F` and `Ctrl+H` via `ShellViewModel` commands
  - 3d: Removed `DocumentOpened` publish from `NewDocument()`; updated test to verify no publish
  - 3e: `ShellViewModel` implements `IDisposable`; stores handlers as fields; unsubscribes in `Dispose()`
- **Commit:** (pending)
- **Closed date:** 2026-06-15
