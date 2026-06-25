using System;
using Aero.Models.Editor;
using Aero.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using DocMsg = Aero.Core;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for a single editor tab.
/// Manages tab title, dirty indicator, close command.
/// </summary>
public class EditorTabViewModel : ReactiveObject, IDisposable
{
    private readonly TextDocument _document;
    private readonly IMessageBus _bus;
    private string _languageId;
    private Action<DocMsg.DocumentModified>? _modificationHandler;
    private Action<DocMsg.DocumentSaved>? _savedHandler;

    /// <summary>
    /// Git status glyph shown in the tab header (e.g. "M" for modified, "A" for added).
    /// Set by EditorViewModel when GitStatusChanged fires.
    /// </summary>
    [Reactive] public string GitStatusGlyph { get; set; } = "";

    public EditorTabViewModel(TextDocument document, IMessageBus bus, string languageId)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _languageId = languageId ?? throw new ArgumentNullException(nameof(languageId));

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

    /// <summary>TextMate language id for syntax highlighting (e.g. "csharp", "plaintext").</summary>
    public string LanguageId
    {
        get => _languageId;
        set
        {
            if (_languageId != value)
            {
                _languageId = value;
                this.RaisePropertyChanged(nameof(LanguageId));
                this.RaisePropertyChanged(nameof(FontFamily));
            }
        }
    }

    /// <summary>Font family based on language — monospace for code, sans-serif for markdown.</summary>
    public string FontFamily => _languageId switch
    {
        // Code languages → monospace
        "csharp" or "fsharp" or "javascript" or "typescript" or
        "typescriptreact" or "javascriptreact" or "python" or
        "html" or "xml" or "xaml" or "json" or "yaml" or
        "css" or "scss" or "bat" or "powershell" or "shell" =>
            "Consolas, Courier New, monospace",

        // Documentation → sans-serif
        "markdown" or "plaintext" => "Inter, Segoe UI, sans-serif",

        // Default to monospace for unknown languages
        _ => "Consolas, Courier New, monospace"
    };

    /// <summary>Whether the document has unsaved changes.</summary>
    public bool IsDirty => _document.IsDirty;

    /// <summary>File-type icon resource key for the tab header.</summary>
    public string Glyph => IconResolver.GetIconKey(FilePath);

    /// <summary>
    /// Resolved <see cref="Geometry"/> for the current <see cref="Glyph"/> key.
    /// Used by XAML bindings that cannot resolve resource keys dynamically.
    /// </summary>
    public Geometry? GlyphGeometry
    {
        get
        {
            if (Application.Current is { } app)
            {
                app.TryFindResource(Glyph, out var resource);
                if (resource is Geometry g)
                    return g;
            }

            return null;
        }
    }
}
