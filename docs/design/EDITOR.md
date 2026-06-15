# Editor Design Notes

## TextBuffer — Data Structure Choice

**Recommendation: Piece Table** (over gap buffer or rope).

A piece table stores the original text + an "add" buffer, with a list of pieces describing which spans of which buffer compose the current document. This gives:

- O(1) undos (just drop pieces from the add buffer)
- Efficient for large files (no copying of original content)
- Simpler to implement than a full rope

Alternative: use AvaloniaEdit's built-in `TextDocument` which is already a piece-table under the hood.

## Editor View

AvaloniaEdit provides `TextEditor` control with:
- Line numbers
- Syntax highlighting (via `HighlightingManager`)
- Folding
- Caret/selection
- Scroll

We'll wrap it in a custom `TextEditorView` that adds:
- Tab header (filename, dirty dot, close button)
- Context menu (cut/copy/paste, go to definition)
- Gutter icons (breakpoints, git diff markers)

## Document Lifecycle

```
User clicks file in tree
    → FileService.ReadAllText(path)
    → new TextDocument { Path = path, Content = text, Language = detect(path) }
    → DocumentManager.OpenDocument(doc)
    → MessageBus.Publish(new DocumentOpened(doc))
    → EditorPanel adds tab, loads TextEditor
    → SyntaxHighlighter applies grammar

User types
    → TextBuffer.Insert/Delete operations
    → TextDocument.IsDirty = true
    → MessageBus.Publish(new DocumentModified(doc))
    → Tab shows dirty dot (*)

User saves (Ctrl+S)
    → FileService.WriteAllText(doc.Path, doc.Content)
    → TextDocument.IsDirty = false
    → MessageBus.Publish(new DocumentSaved(doc))

User closes tab
    → If dirty: show "Save changes?" dialog
    → DocumentManager.CloseDocument(doc)
    → MessageBus.Publish(new DocumentClosed(doc))
```

## Undo / Redo

Command pattern:
```
abstract class UndoableCommand {
    abstract void Undo(TextBuffer buffer);
    abstract void Redo(TextBuffer buffer);
}

class InsertCommand : UndoableCommand { int offset; string text; }
class DeleteCommand : UndoableCommand { int offset; string deletedText; }

class UndoManager {
    Stack<UndoableCommand> undoStack;
    Stack<UndoableCommand> redoStack;
    int groupLevel; // groups consecutive inserts into one undo step
}
```

## Syntax Highlighting

Leverage AvaloniaEdit's `HighlightingManager`:
1. Load `.xshd` (Avalonia syntax definition) or convert TextMate grammars
2. Register per-language: `HighlightingManager.Instance.RegisterHighlighting("C#", ...)`
3. Set on editor: `editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#")`

For LSP semantic tokens (when available), override the coloring with server-provided token types.

## Code Completion

1. User presses Ctrl+Space (or types trigger chars like `.`)
2. `CompletionService.RequestCompletions(document, position)`
3. Sends LSP `textDocument/completion` request
4. Receives `CompletionItem[]` with label, kind, detail, documentation
5. Shows `CompletionWindow` (AvaloniaEdit built-in)
6. On select, applies `TextEdit` or `InsertText` from completion item
