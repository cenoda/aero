using System;
using ReactiveUI;
using Aero.Models.Editor;
using IMessageBus = Aero.Core.IMessageBus;
using DocMsg = Aero.Core;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for a single editor tab.
/// Manages tab title, dirty indicator, close command.
/// </summary>
public class EditorTabViewModel : ReactiveObject, IDisposable
{
    private readonly TextDocument _document;
    private readonly IMessageBus _bus;
    private Action<DocMsg.DocumentModified>? _modificationHandler;
    private Action<DocMsg.DocumentSaved>? _savedHandler;

    public EditorTabViewModel(TextDocument document, IMessageBus bus)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

        // Store handlers for later unsubscribe
        _modificationHandler = msg =>
        {
            if (msg.Document == _document)
            {
                this.RaisePropertyChanged(nameof(IsDirty));
                this.RaisePropertyChanged(nameof(Title));
            }
        };

        _savedHandler = msg =>
        {
            if (msg.Document == _document)
            {
                this.RaisePropertyChanged(nameof(IsDirty));
                this.RaisePropertyChanged(nameof(Title));
            }
        };

        // Subscribe to document modification to refresh dirty state
        // Match by document reference to reliably handle untitled documents
        _bus.Subscribe<DocMsg.DocumentModified>(_modificationHandler);

        // Also listen for save to clear dirty
        _bus.Subscribe<DocMsg.DocumentSaved>(_savedHandler);
    }

    /// <summary>Dispose subscriptions to prevent memory leaks.</summary>
    public void Dispose()
    {
        if (_modificationHandler != null)
            _bus.Unsubscribe<DocMsg.DocumentModified>(_modificationHandler);
        if (_savedHandler != null)
            _bus.Unsubscribe<DocMsg.DocumentSaved>(_savedHandler);
    }

    /// <summary>The document this tab represents.</summary>
    public TextDocument Document => _document;

    /// <summary>Display name for the tab (filename + dirty indicator).</summary>
    public string Title => _document.IsDirty
        ? _document.DisplayName + " *"
        : _document.DisplayName;

    /// <summary>The file path (may be null for new documents).</summary>
    public string? FilePath => _document.FilePath;

    /// <summary>Whether the document has unsaved changes.</summary>
    public bool IsDirty => _document.IsDirty;
}
