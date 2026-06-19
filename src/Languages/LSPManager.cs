using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Editor;
using Aero.Services;

namespace Aero.Languages;

/// <summary>
/// Application-level coordinator for the active LSP session and open document
/// synchronization. Keeps C# editor buffers in sync with a language server via
/// textDocument/didOpen, didChange, didSave, and didClose notifications.
/// </summary>
public sealed class LSPManager : IDisposable
{
    private readonly IMessageBus _bus;
    private readonly DocumentManager _documentManager;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly Func<string, string?, LSPSession> _sessionFactory;
    private readonly TimeSpan _debounce;

    private readonly object _sessionLock = new();
    private readonly HashSet<string> _openUris = new();
    private readonly Dictionary<TextDocument, PendingChange> _pendingChanges = new();
    private readonly object _pendingChangesLock = new();

    private LSPSession? _session;
    private string? _rootFolder;
    private bool _isDisposed;

    private Action<FolderOpened>? _folderOpenedHandler;
    private Action<DocumentOpened>? _documentOpenedHandler;
    private Action<DocumentClosed>? _documentClosedHandler;
    private Action<DocumentSaved>? _documentSavedHandler;
    private Action<DocumentTextChanged>? _documentTextChangedHandler;

    public LSPManager(
        IMessageBus bus,
        DocumentManager documentManager,
        ILanguageDetectionService languageDetection,
        Func<string, string?, LSPSession> sessionFactory,
        TimeSpan debounce)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _debounce = debounce;

        _folderOpenedHandler = OnFolderOpened;
        _documentOpenedHandler = OnDocumentOpened;
        _documentClosedHandler = OnDocumentClosed;
        _documentSavedHandler = OnDocumentSaved;
        _documentTextChangedHandler = OnDocumentTextChanged;

        _bus.Subscribe(_folderOpenedHandler);
        _bus.Subscribe(_documentOpenedHandler);
        _bus.Subscribe(_documentClosedHandler);
        _bus.Subscribe(_documentSavedHandler);
        _bus.Subscribe(_documentTextChangedHandler);
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
        }

        if (_folderOpenedHandler != null)
            _bus.Unsubscribe<FolderOpened>(_folderOpenedHandler);
        if (_documentOpenedHandler != null)
            _bus.Unsubscribe<DocumentOpened>(_documentOpenedHandler);
        if (_documentClosedHandler != null)
            _bus.Unsubscribe<DocumentClosed>(_documentClosedHandler);
        if (_documentSavedHandler != null)
            _bus.Unsubscribe<DocumentSaved>(_documentSavedHandler);
        if (_documentTextChangedHandler != null)
            _bus.Unsubscribe<DocumentTextChanged>(_documentTextChangedHandler);

        CancelAllPendingChanges();

        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
            _rootFolder = null;
            _openUris.Clear();
        }
    }

    private void OnFolderOpened(FolderOpened msg)
    {
        if (_isDisposed)
            return;

        string folderPath;
        try
        {
            folderPath = Path.GetFullPath(msg.Path);
        }
        catch (Exception ex)
        {
            SetStatus($"LSP folder path error: {ex.Message}");
            return;
        }

        lock (_sessionLock)
        {
            if (_session != null && string.Equals(_rootFolder, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                // Idempotent: same folder already open.
                return;
            }
        }

        LSPSession? oldSession;
        lock (_sessionLock)
        {
            oldSession = _session;
            _session = null;
            _rootFolder = null;
            _openUris.Clear();
        }

        try
        {
            oldSession?.Dispose();
        }
        catch (Exception ex)
        {
            SetStatus($"LSP previous session cleanup warning: {ex.Message}");
        }

        CancelAllPendingChanges();

        LSPSession? newSession = null;
        try
        {
            var rootUri = ToFileUri(folderPath);
            newSession = _sessionFactory("csharp-ls", rootUri);
        }
        catch (Exception ex)
        {
            SetStatus($"LSP server unavailable: {ex.Message}");
            return;
        }

        try
        {
            var initialized = newSession.InitializeAsync("csharp-ls", ToFileUri(folderPath), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (!initialized)
            {
                newSession.Dispose();
                return;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"LSP initialization failed: {ex.Message}");
            newSession.Dispose();
            return;
        }

        lock (_sessionLock)
        {
            if (_isDisposed)
            {
                newSession.Dispose();
                return;
            }

            _session = newSession;
            _rootFolder = folderPath;
        }

        SetStatus($"LSP session started for {folderPath}");
    }

    private void OnDocumentOpened(DocumentOpened msg)
    {
        if (_isDisposed)
            return;

        lock (_sessionLock)
        {
            var session = _session;
            if (session == null)
                return;

            var doc = FindDocumentByPath(msg.FilePath);
            if (doc == null)
                return;

            var uri = doc.Uri;
            if (string.IsNullOrEmpty(uri) || _openUris.Contains(uri))
                return;

            var languageId = _languageDetection.Detect(doc.FilePath).Id;
            if (!string.Equals(languageId, "csharp", StringComparison.OrdinalIgnoreCase))
                return;

            _openUris.Add(uri);
            session.SendNotification(
                "textDocument/didOpen",
                new
                {
                    textDocument = new
                    {
                        uri,
                        languageId,
                        version = doc.Version,
                        text = doc.Content,
                    },
                });
        }
    }

    private void OnDocumentTextChanged(DocumentTextChanged msg)
    {
        if (_isDisposed)
            return;

        var doc = msg.Document;
        string? uri;

        lock (_sessionLock)
        {
            var session = _session;
            if (session == null)
                return;

            uri = doc.Uri;
            if (string.IsNullOrEmpty(uri) || !_openUris.Contains(uri))
                return;
        }

        // Capture content synchronously on the UI thread; TextDocument.Content is
        // thread-affine and will throw if read from a worker thread.
        var content = doc.Content;

        lock (_pendingChangesLock)
        {
            if (_pendingChanges.TryGetValue(doc, out var existing))
            {
                existing.Cts.Cancel();
                existing.Cts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _pendingChanges[doc] = new PendingChange { Uri = uri, Content = content, Cts = cts };
            _ = Task.Run(() => SendDidChangeAfterDebounceAsync(doc, uri, content, cts), cts.Token);
        }
    }

    private async Task SendDidChangeAfterDebounceAsync(TextDocument doc, string uri, string content, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_debounce, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Only send if this is still the active pending change for the document.
        lock (_pendingChangesLock)
        {
            if (!_pendingChanges.TryGetValue(doc, out var current) || current.Cts != cts)
                return;
            _pendingChanges.Remove(doc);
        }

        LSPSession? session;
        lock (_sessionLock)
        {
            session = _session;
            if (session == null || !_openUris.Contains(uri))
                return;
        }

        var newVersion = doc.AdvanceVersion();

        session.SendNotification(
            "textDocument/didChange",
            new
            {
                textDocument = new
                {
                    uri,
                    version = newVersion,
                },
                contentChanges = new[]
                {
                    new
                    {
                        text = content,
                    },
                },
            });
    }

    private void OnDocumentSaved(DocumentSaved msg)
    {
        if (_isDisposed)
            return;

        var doc = msg.Document;
        string? uri;

        lock (_sessionLock)
        {
            var session = _session;
            if (session == null)
                return;

            uri = doc.Uri;
            if (string.IsNullOrEmpty(uri) || !_openUris.Contains(uri))
                return;
        }

        // Flush any pending didChange before didSave so the server sees the latest
        // buffer state in the correct order.
        FlushPendingChange(doc);

        lock (_sessionLock)
        {
            var session = _session;
            if (session == null || !_openUris.Contains(uri))
                return;

            session.SendNotification(
                "textDocument/didSave",
                new
                {
                    textDocument = new
                    {
                        uri,
                    },
                });
        }
    }

    private void OnDocumentClosed(DocumentClosed msg)
    {
        if (_isDisposed)
            return;

        var doc = msg.Document;
        CancelPendingChange(doc);

        lock (_sessionLock)
        {
            var session = _session;
            var uri = doc.Uri;
            if (session == null || string.IsNullOrEmpty(uri) || !_openUris.Contains(uri))
                return;

            _openUris.Remove(uri);
            session.SendNotification(
                "textDocument/didClose",
                new
                {
                    textDocument = new
                    {
                        uri,
                    },
                });
        }
    }

    private void FlushPendingChange(TextDocument doc)
    {
        PendingChange? pending;
        lock (_pendingChangesLock)
        {
            if (!_pendingChanges.TryGetValue(doc, out pending))
                return;
            _pendingChanges.Remove(doc);
        }

        pending.Cts.Cancel();
        pending.Cts.Dispose();

        LSPSession? session;
        lock (_sessionLock)
        {
            session = _session;
            if (session == null || string.IsNullOrEmpty(pending.Uri) || !_openUris.Contains(pending.Uri))
                return;
        }

        var newVersion = doc.AdvanceVersion();

        session.SendNotification(
            "textDocument/didChange",
            new
            {
                textDocument = new
                {
                    pending.Uri,
                    version = newVersion,
                },
                contentChanges = new[]
                {
                    new
                    {
                        text = pending.Content,
                    },
                },
            });
    }

    private void CancelPendingChange(TextDocument doc)
    {
        lock (_pendingChangesLock)
        {
            if (!_pendingChanges.TryGetValue(doc, out var pending))
                return;
            _pendingChanges.Remove(doc);
            pending.Cts.Cancel();
            pending.Cts.Dispose();
        }
    }

    private void CancelAllPendingChanges()
    {
        lock (_pendingChangesLock)
        {
            foreach (var pending in _pendingChanges.Values)
            {
                pending.Cts.Cancel();
                pending.Cts.Dispose();
            }
            _pendingChanges.Clear();
        }
    }

    private TextDocument? FindDocumentByPath(string? filePath)
    {
        foreach (var doc in _documentManager.Documents)
        {
            if (doc.FilePath == filePath)
                return doc;
        }
        return null;
    }

    private static string ToFileUri(string path)
    {
        return new Uri(Path.GetFullPath(path), UriKind.Absolute).AbsoluteUri;
    }

    private void SetStatus(string message)
    {
        _bus.Publish(new StatusMessage(message));
    }

    private sealed class PendingChange
    {
        public string Uri { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public CancellationTokenSource Cts { get; set; } = new();
    }
}
