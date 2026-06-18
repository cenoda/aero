# Phase 1 — To Fix (ARCHIVED)

> **Status:** All items completed. Round 4 (final verification) closed 2026-06-18; Rounds 1–3 archived 2026-06-17.

Code quality issues found during Phase 1 review. Must be resolved before Phase 2 starts.

---

## Round 4 — Final Verification Review (2026-06-18)

- [x] **R4.1 `TextDocument.Content` swallows exceptions and can return stale data** *(notable)*
  The `Content` getter/setter wrap `_document.Text` in `try/catch (InvalidOperationException)` and fall back to a cached `_content` field, purely to let unit tests read content off the UI thread (AvaloniaEdit's `VerifyAccess()` throws on non-UI threads). Problem: `_content` is only refreshed on `set`. Real edits flow through the AvaloniaEdit control on the UI thread and never update `_content`, so any off-thread read of `Content` after an edit silently returns **stale text** — a latent data-loss trap. Today `SaveDocumentAsync` reads it on the UI thread so it works by luck.
  Fix: remove the silent `catch` from the model. Make tests pump an Avalonia dispatcher (or use a headless dispatcher) instead of distorting production code, or introduce an explicit test seam. The model should not hide thread-affinity bugs.
  **Fixed (2026-06-18):** Removed `_content` cache and both `try/catch` blocks — `Content` now delegates directly to `_document.Text`, so off-thread access fails fast instead of returning stale data. Verified production only reads `Content` on the UI thread (`SaveAsync`/`SaveAsAsync` evaluate it synchronously before the I/O await on the ReactiveUI main scheduler). Tests fixed by adding a single-thread async pump (`tests/Stubs/SingleThread.cs`) and wrapping the 4 async tests that access document content across thread hops, so AvaloniaEdit's thread affinity is honoured without distorting the model.

- [x] **R4.2 `EditorViewModel.Dispose()` is never called — the leak fix (TOFIX #1) is dead code** *(notable)*
  `EditorViewModel` is a DI singleton. `ShellViewModel.Dispose()` runs on exit but does not dispose its child `EditorViewModel`, and the `ServiceProvider` itself is never disposed. So the `IDisposable` implementation added for Round-1 item #1 never actually runs. Harmless today (the VM lives for the whole app lifetime, so the re-subscription leak it guards against cannot occur on a singleton), but the fix gives false comfort. Note: `EditorTabViewModel.Dispose()` *is* correctly invoked on tab close — that is the one that matters.
  Fix: either wire it (`ShellViewModel.Dispose()` → `_editorViewModel.Dispose()`, and dispose the `ServiceProvider` on shutdown), or add a comment stating the disposal is defensive-only for a singleton and intentionally not wired.
  **Fixed (2026-06-18):** `App` now disposes the DI `ServiceProvider` on `desktop.Exit`, so the container tears down all `IDisposable` singletons (`ShellViewModel`, `EditorViewModel`) on both exit paths (Exit menu command and window "X" button). Made `EditorViewModel.Dispose()` and `ShellViewModel.Dispose()` idempotent with a `_disposed` guard, since the Exit command also disposes `ShellViewModel` manually before shutdown.

- [x] **R4.3 Static `App.Services` locator contradicts AGENTS.md** *(minor)*
  AGENTS.md §5 prohibits "Manual ServiceLocator" and "Static service access". `App.Services` exists as an `internal static` accessor but is never referenced anywhere in `src/`. Dead code that violates the project's own convention.
  Fix: delete the `Services` static property from `App.axaml.cs`.

- [x] **R4.4 Unnecessary `using` directives** *(minor)*
  Analyzer hints (no build impact):
  - `ShellViewModel.cs`: `using Avalonia.Input;`, `using Avalonia.Controls;`
  - `DocumentManager.cs`: `using System.Collections.ObjectModel;`
  - `EditorTabViewModel.cs`: `using Aero.Core;`, `using ReactiveUI.Fody.Helpers;`
  - `MainWindow.axaml.cs`: `using System.Threading.Tasks;`
  Fix: remove the unused usings.

- [x] **R4.5 Inconsistent message nullability** *(minor)*
  `DocumentClosed.Document` is non-nullable while `DocumentModified.Document` and `DocumentSaved.Document` are nullable (`TextDocument?`). Same conceptual payload, different contracts.
  Fix: pick one convention across the editor messages in `Messages.cs` (non-nullable is preferable since publishers always pass a real document).

- [x] **R4.6 `CheckDirtyBeforeExitAsync` can hang if `ConfirmDirtyClose` has no subscriber** *(minor / theoretical)*
  `ShellViewModel.CheckDirtyBeforeExitAsync` publishes `ConfirmDirtyClose` and awaits a `TaskCompletionSource` that only completes inside the subscriber's response callback. If `MainWindow.Initialize()` was never called (no subscriber), the await never completes and exit hangs forever. In practice MainWindow always subscribes, so this is theoretical.
  Fix: guard with a timeout, or check that at least one subscriber exists, or default to a safe response when unhandled.

- [x] **R4.7 Unused public API surface on `TextDocument`** *(minor / optional)*
  `GetColumn`, `GetLineNumber`, `GetLineAt`, `SelectedText`, `SelectionStart`/`SelectionLength`, `CanUndo`/`CanRedo` appear unused in Phase 1. Not wrong, but it is API ahead of need that must be maintained.
  Fix: trim to what's used, or leave with a note if intentionally reserved for Phase 2+.

---

## Round 1–3 (ARCHIVED — completed 2026-06-17)

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
