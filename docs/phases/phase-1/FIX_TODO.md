# Phase 1 Fixes TODO

## High Priority
- [x] Create tracking file
- [x] 1.1 FindReplaceOverlay - Add to EditorView UI
- [x] 1.2 EditorView.axaml.cs - Wire SetActions() callback
- [x] 1.3 EditorViewModel - Implement FindNext/ReplaceNext/ReplaceAll
- [x] 1.4 ShellViewModel - Hook Find/Replace commands to show/hide overlay

- [x] 2.1 EditorView.axaml - Add close button (×) to tab template
- [x] 2.2 EditorTabViewModel - Add CloseTabCommand
- [x] 2.3 EditorViewModel - Handle CloseTab properly

## Medium Priority
- [x] 3. OnDocumentClosed - Match by document reference (DocumentClosed message now carries TextDocument)
- [x] 4. App.Services - Make internal instead of public static

## Lower Priority
- [ ] 5. EditorTabViewModel - Unsubscribe from MessageBus on dispose
- [x] 6. ShellViewModel - Remove duplicated Language property (now uses EditorViewModel.Language)
- [x] 7. DocumentManager - Implement untitled naming (Untitled, Untitled-2, ...)

## Post-Review Fixes (Round 3)
- [x] TextDocument.IsNew - Replaced fragile StartsWith("Untitled") hack with explicit `_isNew` bool flag
- [x] TextDocument - Added `DisplayName` property; untitled docs use DisplayName, not FilePath
- [x] DocumentManager.NewDocument - Uses DisplayName instead of fake FilePath (prevents SaveAsync IOException)
- [x] DocumentClosed message - Now carries TextDocument reference for reliable tab matching
- [x] FindInText whole-word - Removed dead `words` variable; fixed infinite-loop bug in non-matching advance logic
