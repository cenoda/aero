# Phase 1 — To Fix

Code quality issues found during Phase 1 review. Must be resolved before Phase 2 starts.

---

- [x] **1. `EditorViewModel` never unsubscribes from MessageBus**
  Subscribes to 5 messages in constructor but doesn't implement `IDisposable` or call `Unsubscribe`. Same leak pattern that was fixed in `EditorTabViewModel` (PHASE1_FIXES step 1). Fix: implement `IDisposable`, store handlers, unsubscribe in `Dispose()`.

- [x] **2. `FindReplaceOverlay` never focuses the Replace field**
  `FocusReplaceOnOpen` is set on `FindReplaceViewModel` when Ctrl+H is pressed, but no code-behind wiring reads it to actually focus `ReplaceTextBox`. The property is effectively dead. Fix: wire it in `FindReplaceOverlay.axaml.cs` on `IsVisibleChanged`.

- [x] **3. `EditorView` leaks `FindReplaceRequested` subscription**
  `vm.FindReplaceRequested += OnFindReplaceRequested` is added on every `DataContextChanged` with no corresponding `-=`. If `DataContext` is ever reassigned the handler stacks up. Fix: unsubscribe in `OnDataContextChanged` before resubscribing.

- [x] **4. App exit doesn't check for dirty documents**
  `ShellViewModel.Exit()` calls `desktop.Shutdown()` immediately. If any tab has unsaved changes they are silently discarded. Fix: iterate open documents, run the same dirty-check flow as `CloseTab` before shutting down.

- [x] **5. `DocumentManager._lastDirtyState` leaks on app exit**
  Entries are removed in `CloseDocument` but if the app exits with open documents the dictionary is never cleaned. Low severity (process exits anyway) but worth noting. Fix: either clear on shutdown or remove the dict entirely if `MarkDirty` can derive previous state without it.
