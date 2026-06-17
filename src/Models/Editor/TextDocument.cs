using System;
using AvaloniaEdit.Document;

namespace Aero.Models.Editor;

/// <summary>
/// Represents an open document in the editor.
/// Wraps AvaloniaEdit's TextDocument with additional metadata.
/// Properties like caret/selection are managed by the TextEditor control,
/// not the underlying document.
/// </summary>
public class TextDocument
{
    private readonly AvaloniaEdit.Document.TextDocument _document;
    private string? _filePath;
    private bool _isNew = true;
    private string _language = "Plain Text";
    private string _displayName = "Untitled";
    private string _content = string.Empty;

    public TextDocument() : this(string.Empty)
    {
    }

    public TextDocument(string content)
    {
        _content = content ?? string.Empty;
        _document = new AvaloniaEdit.Document.TextDocument(_content);
    }

    public TextDocument(string content, string filePath) : this(content)
    {
        _filePath = filePath;
        _isNew = false;
        _displayName = System.IO.Path.GetFileName(filePath);
    }

    /// <summary>The underlying AvaloniaEdit document.</summary>
    public AvaloniaEdit.Document.TextDocument InnerDocument => _document;

    /// <summary>Full path to the file (null for new unsaved documents).</summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                _isNew = false;
                _displayName = System.IO.Path.GetFileName(value);
            }
        }
    }

    /// <summary>Display name used in tabs (filename or "Untitled", "Untitled-2", etc.).</summary>
    public string DisplayName
    {
        get => _displayName;
        set => _displayName = value;
    }

    /// <summary>
    /// Whether the document has unsaved changes.
    /// Derived from the undo stack's "original file" marker, so undoing
    /// back to the saved state automatically clears this flag.
    /// </summary>
    public bool IsDirty => !_document.UndoStack.IsOriginalFile;

    /// <summary>Whether this is a new unsaved document.</summary>
    public bool IsNew => _isNew;

    /// <summary>Detected or set language for syntax highlighting.</summary>
    public string Language
    {
        get => _language;
        set => _language = value;
    }

    /// <summary>
    /// The text content. When accessed from a non-UI thread (e.g. unit tests
    /// without an Avalonia dispatcher), falls back to a cached plain-text copy
    /// to avoid AvaloniaEdit.TextDocument.VerifyAccess() failures.
    /// </summary>
    public string Content
    {
        get
        {
            try { return _document.Text; }
            catch (InvalidOperationException) { return _content; }
        }
        set
        {
            _content = value ?? string.Empty;
            try { _document.Text = _content; }
            catch (InvalidOperationException) { /* non-UI thread — keep _content */ }
        }
    }

    /// <summary>Current caret offset in the document (managed by TextEditor, updated externally).</summary>
    public int CaretOffset { get; set; }

    /// <summary>Starting offset of the selection (managed by TextEditor, updated externally).</summary>
    public int SelectionStart { get; set; }

    /// <summary>Length of the selection (managed by TextEditor, updated externally).</summary>
    public int SelectionLength { get; set; }

    /// <summary>Whether there is a selection.</summary>
    public bool HasSelection => SelectionLength > 0;

    /// <summary>Selected text (if any, from document).</summary>
    public string SelectedText => HasSelection 
        ? _document.GetText(SelectionStart, SelectionLength) 
        : string.Empty;

    /// <summary>Undo stack for this document.</summary>
    public AvaloniaEdit.Document.UndoStack UndoStack => _document.UndoStack;

    /// <summary>Can undo?</summary>
    public bool CanUndo => _document.UndoStack.CanUndo;

    /// <summary>Can redo?</summary>
    public bool CanRedo => _document.UndoStack.CanRedo;

    /// <summary>Undo the last action.</summary>
    public void Undo() => _document.UndoStack.Undo();

    /// <summary>Redo the last undone action.</summary>
    public void Redo() => _document.UndoStack.Redo();

    /// <summary>Mark the document as clean (after save).</summary>
    public void MarkAsClean()
    {
        // MarkAsOriginalFile preserves undo history (VS Code behavior)
        // and makes IsDirty (which is derived from IsOriginalFile) report false.
        _document.UndoStack.MarkAsOriginalFile();
    }

    /// <summary>Get line at offset.</summary>
    public DocumentLine GetLineAt(int offset) => _document.GetLineByOffset(offset);

    /// <summary>Get current line number from offset.</summary>
    public int GetLineNumber(int offset) => _document.GetLineByOffset(offset).LineNumber;

    /// <summary>Get column from offset (position within line).</summary>
    public int GetColumn(int offset)
    {
        var line = _document.GetLineByOffset(offset);
        return offset - line.Offset + 1;
    }

    /// <summary>Get line and column from offset.</summary>
    public (int Line, int Column) GetLineColumn(int offset)
    {
        var line = _document.GetLineByOffset(offset);
        return (line.LineNumber, offset - line.Offset + 1);
    }
}
