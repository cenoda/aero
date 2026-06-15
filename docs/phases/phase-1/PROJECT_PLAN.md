# Phase 1: The Editor — Implementation Plan

## Overview

Phase 1 implements the core text editor with tabs, open/save, undo/redo, and find/replace. This is the heart of the IDE.

## Information Gathered

### Entry Condition
- Phase 0 complete: DI container, AvaloniaEdit package, directory skeleton all in place
- Current setup: Avalonia 11.2, ReactiveUI, Microsoft.Extensions.DependencyInjection
- Libraries already available: AvaloniaEdit 11.3.*, Dock.Avalonia, Material.Icons.Avalonia

### Exit Conditions (Must All Pass)
1. Can open, edit, save, and close multiple files via tabs
2. Undo/Redo works across all open files
3. Find/Replace overlay functions correctly
4. Status bar shows cursor position (Ln X, Col Y)

---

## Detailed Implementation Plan

### Phase 1 is divided into 8 checklist items, implemented in logical order:

### Step 1: Document Model
**Files to create:**
- `src/Models/Editor/TextDocument.cs` — document model with properties: FilePath, Content, IsDirty, IsNew, Language, CaretOffset, SelectionStart, SelectionLength, UndoRedoStack
- `src/Models/Editor/IDocument.cs` — interface for document abstraction

**Properties:**
- `FilePath` — full path to file (null for new unsaved files)
- `Content` — text content
- `IsDirty` — modified flag (syncs with tab header dot)
- `IsNew` — true for new unsaved files
- `Language` — detected language for syntax highlighting
- `CaretOffset` — caret position
- `SelectionStart` / `SelectionLength` — selection range
- `Encoding` — file encoding (UTF-8 default)

### Step 2: Document Manager Service
**Files to create:**
- `src/Services/DocumentManager.cs` — manages open documents, file I/O
- `src/Services/FileDialogService.cs` — file open/save dialogs

**Responsibilities:**
- Track open documents (collection)
- Handle open/close/save operations
- Dirty flag management
- Publish messages: DocumentOpened, DocumentClosed, ActiveDocumentChanged, DocumentModified, DocumentSaved

**Messages to add (extend `src/Core/Messages.cs`):**
```csharp
public record DocumentModified(string FilePath);
public record DocumentSaved(string FilePath);
```

### Step 3: Undo/Redo System
**Implementation note:** Per EDITOR.md recommendation, use AvaloniaEdit's built-in UndoStack. The custom command classes (UndoableCommand.cs, InsertCommand.cs, DeleteCommand.cs) are deferred to when they're actually needed for operations outside AvaloniaEdit's scope.

**Implementation:**
- Enable AvaloniaEdit's UndoStack on each TextDocument
- Keyboard shortcuts: Ctrl+Z (Undo), Ctrl+Y (Redo)
- Per-document undo stack (not global)
- No additional files needed for Phase 1

### Step 4: Editor View (AvaloniaEdit Integration)
**Files to modify:**
- `src/Views/EditorView.axaml` — new editor panel with TextEditor
- `src/Views/EditorView.axaml.cs` — code-behind
- `src/ViewModels/EditorViewModel.cs` — editor panel ViewModel

**Implementation:**
- Use AvaloniaEdit's `TextEditor` control with line numbers
- Wire up to TextDocument
- Bind to DocumentManager's active document
- Show line numbers (enabled by default)

### Step 5: Tabbed Editor
**Files to modify:**
- `MainWindow.axaml` — add TabControl for documents
- `MainWindow.axaml.cs` — handle tab events
- `src/ViewModels/ShellViewModel.cs` — add tabs collection

**Implementation:**
- TabControl with TabItem per open document
- Tab shows: filename + dirty indicator (*) + close button
- Click tab to switch active document
- Close button (×) on each tab
- Middle-click to close

### Step 6: File Open/Save (Ctrl+O, Ctrl+S)
**Implementation:**
- Add menu bar: File menu with Open, Save, Save As, Close
- Keyboard shortcuts: Ctrl+O, Ctrl+S, Ctrl+Shift+S
- File dialogs using Avalonia's `FilePickerManager`
- Update Window title: "Aero - {filename}" or "Aero - {filename}*"

### Step 7: Find/Replace
**Files to create:**
- `src/Views/FindReplaceOverlay.axaml` — overlay panel (Ctrl+F)
- `src/Views/FindReplaceOverlay.axaml.cs`
- `src/ViewModels/FindReplaceViewModel.cs`

**Features:**
- Find text input
- Replace text input
- Find Next / Replace / Replace All buttons
- Case sensitivity toggle
- Whole word toggle
- Close on Escape

### Step 8: Status Bar (Cursor Position)
**Implementation:**
- Add status bar to MainWindow.axaml
- Show: Line X, Col Y (position from active document)
- Show: file encoding, line ending type
- Show: language mode (e.g., "C#")
- Update on caret movement

---

## UI Layout Changes

### MainWindow.axaml → New Layout:
```
DockPanel
├── MenuBar (File, Edit, View, Help)
├── Toolbar (Open, Save, Undo, Redo, Find)
├── Main Content (EditorPanel with Tabs)
│   └── TabControl → TextEditor per tab
├── StatusBar
│   ├── Ln X, Col Y
│   ├── Encoding
│   ├── Language
│   └── Dirty indicator
```

---

## Key Bindings

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open file |
| Ctrl+S | Save |
| Ctrl+Shift+S | Save As |
| Ctrl+W | Close tab |
| Ctrl+F4 | Close tab |
| Ctrl+Tab | Next tab |
| Ctrl+Shift+Tab | Previous tab |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+F | Find/Replace |
| Escape | Close Find/Replace |
| Ctrl+N | New file |

---

## File Structure After Phase 1

```
src/
├── Models/Editor/
│   ├── TextDocument.cs (new)
│   ├── IDocument.cs (new)
│   ├── UndoableCommand.cs (new)
│   ├── InsertCommand.cs (new)
│   └── DeleteCommand.cs (new)
├── Services/
│   ├── DocumentManager.cs (new)
│   └── FileDialogService.cs (new)
│   └── UndoRedoManager.cs (new)
├── ViewModels/
│   ├── EditorViewModel.cs (new)
│   ├── EditorTabViewModel.cs (new)
│   └── FindReplaceViewModel.cs (new)
├── Views/
│   ├── EditorView.axaml (new)
│   ├── EditorView.axaml.cs (new)
│   ├── FindReplaceOverlay.axaml (new)
│   └── FindReplaceOverlay.axaml.cs (new)
├── MainWindow.axaml (modify)
├── MainWindow.axaml.cs (modify)
└── App.axaml.cs (modify - register services)
```

---

## Tests to Add

- `src/Tests/` directory (create)
- `DocumentManagerTests.cs` — open, close, dirty flag
- `UndoRedoManagerTests.cs` — undo/redo stack

---

## Dependent Files to Edit

1. `src/Core/Messages.cs` — add DocumentModified, DocumentSaved
2. `src/MainWindow.axaml` — major layout changes
3. `src/MainWindow.axaml.cs` — event handlers
4. `src/App.axaml.cs` — register new services
5. `src/ViewModels/ShellViewModel.cs` — add document management

---

## Follow-up Steps

1. Build and verify: `dotnet run --project src`
2. Test opening multiple files
3. Test undo/redo across tabs
4. Test find/replace
5. Update `docs/roadmap/PHASES.md` with completed items [x]
6. Commit: `editor: implement Phase 1 — text editor with tabs`

---

## Notes

- Use AvaloniaEdit's built-in TextDocument and undo stack where possible
- Dirty flag must sync with tab header and window title
- Keep ViewModels focused — don't let them reference Views directly
- Use MessageBus for all cross-component communication
