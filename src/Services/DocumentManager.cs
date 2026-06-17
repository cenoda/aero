using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Editor;

namespace Aero.Services;

/// <summary>
/// Manages open documents in the editor.
/// Handles open, close, save operations and tracks active document.
/// </summary>
public class DocumentManager
{
    private readonly IMessageBus _bus;
    private readonly List<TextDocument> _documents = new();
    private readonly Dictionary<TextDocument, bool> _lastDirtyState = new();
    private TextDocument? _activeDocument;

    public DocumentManager(IMessageBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    /// <summary>All open documents.</summary>
    public IReadOnlyList<TextDocument> Documents => _documents.AsReadOnly();

    /// <summary>The currently active document (or null).</summary>
    public TextDocument? ActiveDocument
    {
        get => _activeDocument;
        private set
        {
            if (_activeDocument != value)
            {
                _activeDocument = value;
                _bus.Publish(new ActiveDocumentChanged(value));
            }
        }
    }

    /// <summary>Open a document from file path.</summary>
    public async Task<TextDocument> OpenDocumentAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        // Check if already open
        var existing = _documents.FirstOrDefault(d => d.FilePath == filePath);
        if (existing != null)
        {
            ActiveDocument = existing;
            return existing;
        }

        // Read file content
        var content = await File.ReadAllTextAsync(filePath);

        // Create document
        var doc = new TextDocument(content, filePath);
        doc.Language = DetectLanguage(filePath);

        _documents.Add(doc);
        ActiveDocument = doc;

        _bus.Publish(new DocumentOpened(filePath));
        return doc;
    }

    /// <summary>Create a new empty document.</summary>
    public TextDocument NewDocument()
    {
        var doc = new TextDocument();

        // Generate a unique display name — track the highest index used rather
        // than counting current documents to avoid duplicates after close+new.
        var maxIndex = _documents
            .Where(d => d.IsNew)
            .Select(d =>
            {
                if (d.DisplayName == "Untitled") return 1;
                if (d.DisplayName.StartsWith("Untitled-")
                    && int.TryParse(d.DisplayName.AsSpan("Untitled-".Length), out var n))
                    return n;
                return 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        var newIndex = maxIndex + 1;
        doc.DisplayName = newIndex == 1 ? "Untitled" : $"Untitled-{newIndex}";
        doc.Language = "Plain Text";

        _documents.Add(doc);
        ActiveDocument = doc;

        // DocumentOpened is not published for untitled documents — the FilePath field
        // in the message record implies a real file path and would mislead subscribers.
        // The EditorViewModel already handles tab creation via NewFile() → EnsureTabForDocument().
        return doc;
    }

    /// <summary>Save the active document.</summary>
    /// <returns>
    /// <c>true</c> if the document was saved; <c>false</c> if the document is new
    /// and the caller must prompt for a path (Save As).
    /// </returns>
    public async Task<bool> SaveDocumentAsync(TextDocument? document)
    {
        var doc = document ?? ActiveDocument;
        if (doc == null)
            return true;

        if (doc.IsNew)
        {
            // New file - caller must prompt for a path.
            return false;
        }

        var filePath = doc.FilePath ?? throw new InvalidOperationException("Document file path is null.");
        await File.WriteAllTextAsync(filePath, doc.Content);
        doc.MarkAsClean();

        _bus.Publish(new DocumentSaved(filePath, doc));
        return true;
    }

    /// <summary>Save document to a new path.</summary>
    public async Task SaveDocumentAsAsync(TextDocument document, string filePath)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        await File.WriteAllTextAsync(filePath, document.Content);
        document.FilePath = filePath;
        document.Language = DetectLanguage(filePath);
        document.MarkAsClean();

        _bus.Publish(new DocumentSaved(filePath, document));
    }

    /// <summary>Close a document.</summary>
    public void CloseDocument(TextDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (!_documents.Remove(document))
            return;

        _lastDirtyState.Remove(document);

        // If closing active, switch to another
        if (ActiveDocument == document)
        {
            ActiveDocument = _documents.FirstOrDefault();
        }

        _bus.Publish(new DocumentClosed(document.FilePath ?? document.DisplayName, document));
    }

    /// <summary>Set a document as active (focused).</summary>
    public void ActivateDocument(TextDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (!_documents.Contains(document))
            throw new InvalidOperationException("Document is not open.");

        ActiveDocument = document;
    }

    /// <summary>
    /// Notify that a document's text changed. Publishes <see cref="DocumentModified"/>
    /// only on dirty-state transitions (clean↔dirty), including the undo-back-to-clean
    /// case which the undo stack's <c>IsOriginalFile</c> marker now detects automatically.
    /// </summary>
    public void MarkDirty(TextDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        bool currentDirty = document.IsDirty;
        bool? previousDirty = _lastDirtyState.TryGetValue(document, out var prev) ? prev : null;

        if (previousDirty != currentDirty)
        {
            _lastDirtyState[document] = currentDirty;
            _bus.Publish(new DocumentModified(document.FilePath ?? document.DisplayName, document));
        }
    }

    /// <summary>Clear the last dirty state dictionary (called on app exit).</summary>
    public void ClearLastDirtyState()
    {
        _lastDirtyState.Clear();
    }

    /// <summary>Detect language from file extension.</summary>
    public static string DetectLanguage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "Plain Text";

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "C#",
            ".fs" => "F#",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".json" => "JSON",
            ".xml" => "XML",
            ".xaml" => "XAML",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".scss" => "SCSS",
            ".md" => "Markdown",
            ".yaml" or ".yml" => "YAML",
            ".sql" => "SQL",
            ".py" => "Python",
            ".rs" => "Rust",
            ".go" => "Go",
            ".java" => "Java",
            ".cpp" or ".cc" or ".cxx" => "C++",
            ".c" or ".h" => "C",
            ".sh" or ".bash" => "Bash",
            ".ps1" => "PowerShell",
            _ => "Plain Text"
        };
    }
}
