# Phase 1 Post-Review Fixes (ARCHIVED)

> **Status:** All items completed. Archived 2026-06-17.

## TODO List

- [x] 1. Fix EditorTabViewModel memory leak - implement IDisposable and unsubscribe from MessageBus
- [x] 2. Update DocumentModified/DocumentSaved messages to include TextDocument reference
- [x] 3. Fix ShellViewModel.Exit() - implement actual shutdown logic
- [x] 4. Fix MVVM violation - move ActiveEditor reference from EditorViewModel to code-behind
- [x] 5. Fix FindReplaceOverlay layout - use Grid overlay instead of DockPanel

## Progress Tracking

### Step 1: EditorTabViewModel Memory Leak Fix ✅
- [x] Implement IDisposable in EditorTabViewModel
- [x] Store subscription handlers for unsubscribe
- [x] Call Dispose() in EditorViewModel.CloseTab and CloseActiveTab

### Step 2: Message Bus Identity Fix ✅
- [x] Update DocumentModified message to include TextDocument reference
- [x] Update DocumentSaved message to include TextDocument reference
- [x] Update EditorTabViewModel subscription to match by document reference
- [x] Update DocumentManager to publish messages with document reference
- [x] Update EditorViewModel to match by document reference

### Step 3: Shell Exit Fix ✅
- [x] Implement Exit() method with IClassicDesktopStyleApplicationLifetime.Shutdown()

### Step 4: MVVM Violation Fix ✅
- [x] Fix race condition with async retry for lazy-loaded TabControl content
- [x] Use exponential backoff retry (10ms, 25ms, 50ms, 100ms, 200ms) to find TextEditor
- [x] Add cancellation support to cancel pending resubscribe when tab changes again
- [x] Verify tab identity before subscribing to prevent stale editor subscriptions

### Step 5: FindReplaceOverlay Layout Fix ✅
- [x] Update EditorView.axaml to use Grid instead of DockPanel
