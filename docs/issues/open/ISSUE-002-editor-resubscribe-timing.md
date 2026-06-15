# ISSUE-002: EditorView tab-switch caret tracking relies on polling — fragile under load

**Label:** BUG
**Status:** open
**Priority:** medium
**Reported by:** code review (Phase 1)
**Assigned to:** —
**Related:** Phase 1 review, `EditorView.axaml.cs:ResubscribeEditorAsync`, `MainWindow.axaml`

## Description

After switching tabs, `EditorView` uses an async retry loop with exponential backoff
(10 ms → 25 → 50 → 100 → 200 ms, ~5 attempts) to find the new `TextEditor` in the
visual tree via `FindDescendantOfType<TextEditor>()`.

If the retry window (~400 ms total) is missed — e.g. under CPU load, slow redraw, or
a future layout change — the caret-position and dirty-state subscriptions are silently
lost. The status bar freezes on the previous position and `NotifyTextChanged` stops firing.

Additionally, `FindDescendantOfType<TextEditor>()` finds the *first* `TextEditor` in the
visual tree. During a tab transition, a virtualized container could briefly hold two editors,
causing the subscription to latch onto the outgoing tab's editor.

## Steps to Reproduce

1. Open two files
2. Rapidly switch between tabs several times
3. Observe the status bar — cursor position may stop updating

(Difficult to reproduce consistently on fast hardware; more likely under system load.)

## Notes

- The root problem is that `TabControl` with a `ContentTemplate` lazily creates the
  `TextEditor` control after the `SelectedItem` binding fires, so there is a window
  where the visual tree doesn't yet contain the new editor.
- Cleaner approaches (no polling required):
  - **Option A:** Give each `TextEditor` an `x:Name` keyed to the tab and retrieve it
    via the `ContentPresenter` after `TabControl.SelectionChanged`.
  - **Option B:** Replace `TabControl.ContentTemplate` with a custom panel that keeps
    all editors in the tree (hidden when inactive) — eliminates the lazy-load window entirely.
  - **Option C:** Use `TabControl.SelectionChanged` event + `Dispatcher.UIThread.Post`
    with `DispatcherPriority.Loaded` to access the visual tree after layout has run,
    instead of a timed retry.
- Option C is the minimal-change fix; Option B is most robust for Phase 2+.

## Debug Log

> Not yet attempted.

## Resolution

- **Root cause:** —
- **Fix:** —
- **Commit:** —
- **Closed date:** —
