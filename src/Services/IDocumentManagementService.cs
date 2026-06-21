using System.Collections.Generic;
using System.Threading.Tasks;
using Aero.Models.Editor;

namespace Aero.Services;

/// <summary>
/// Manages open documents in the editor.
/// Handles open, close, save operations and tracks active document.
/// </summary>
public interface IDocumentManagementService
{
    /// <summary>All open documents.</summary>
    IReadOnlyList<TextDocument> Documents { get; }

    /// <summary>The currently active document (or null).</summary>
    TextDocument? ActiveDocument { get; }

    /// <summary>Open a document from file path.</summary>
    Task<TextDocument> OpenDocumentAsync(string filePath);

    /// <summary>Create a new empty document.</summary>
    TextDocument NewDocument();

    /// <summary>Save the active document.</summary>
    Task<bool> SaveDocumentAsync(TextDocument? document);

    /// <summary>Save document to a new path.</summary>
    Task SaveDocumentAsAsync(TextDocument document, string filePath);

    /// <summary>Close a document.</summary>
    void CloseDocument(TextDocument document);

    /// <summary>Set a document as active (focused).</summary>
    void ActivateDocument(TextDocument document);

    /// <summary>
    /// Notify that a document's text changed. Publishes <see cref="Core.DocumentModified"/>
    /// only on dirty-state transitions (clean↔dirty).
    /// </summary>
    void MarkDirty(TextDocument document);

    /// <summary>Clear the last dirty state dictionary (called on app exit).</summary>
    void ClearLastDirtyState();
}