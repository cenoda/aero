# Language Server Protocol (LSP) Design

## Architecture

```
User types in editor
    → TextEditorViewModel sends LSP request
    → LSPManager routes to correct LSPSession
    → LSPSession sends JSON-RPC over stdin/stdout
    → Language Server process responds
    → LSPSession deserializes response
    → ViewModel updates (diagnostics, completions, hover)
```

## JSON-RPC Protocol

All LSP communication uses JSON-RPC 2.0 over stdio:

```json
// Request (client → server)
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "textDocument/completion",
  "params": { ... }
}

// Response (server → client)
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": [ ... ]
}

// Notification (either direction, no id)
{
  "jsonrpc": "2.0",
  "method": "textDocument/publishDiagnostics",
  "params": { ... }
}
```

## LSPSession

Manages one language server process:

```csharp
class LSPSession : IDisposable {
    Process _process;         // the LSP server process
    StreamWriter _writer;     // stdin → server
    StreamReader _reader;     // stdout → server
    int _nextId;
    Dictionary<int, TaskCompletionSource<JsonElement>> _pending;

    // LSP lifecycle
    Task InitializeAsync(string rootPath);
    Task ShutdownAsync();

    // Generic request
    Task<T> SendRequest<T>(string method, object? params);
    void SendNotification(string method, object? params);

    // Events for server-initiated notifications
    event Action<DiagnosticsParams>? OnPublishDiagnostics;
    event Action<LogMessageParams>? OnLogMessage;
}
```

## LSP Manager

Spawns and manages sessions per language:

```csharp
class LSPManager {
    Dictionary<string, LSPSession> _sessions; // key = languageId

    async Task<LSPSession> GetSession(string languageId, string rootPath) {
        if (!_sessions.ContainsKey(languageId)) {
            var session = new LSPSession();
            await session.InitializeAsync(rootPath, GetServerCommand(languageId));
            _sessions[languageId] = session;
        }
        return _sessions[languageId];
    }
}
```

## Language Server Commands

| Language | Server Binary                                   |
|----------|-------------------------------------------------|
| C#       | `csharp-ls` or `omnisharp`                      |
| JSON     | `vscode-json-languageserver`                    |
| HTML/CSS | `vscode-html-languageserver` / `vscode-css-ls` |
| TypeScript | `typescript-language-server`                  |
| Python   | `pylsp` or `pyright`                            |
| Rust     | `rust-analyzer`                                 |
| Go       | `gopls`                                         |

## Key LSP Features (by phase)

| Feature                    | LSP Method                       |
|----------------------------|----------------------------------|
| Diagnostics (red squiggly) | `textDocument/publishDiagnostics` (notification) |
| Completion (Ctrl+Space)    | `textDocument/completion`        |
| Hover (tooltip)            | `textDocument/hover`             |
| Go to Definition (F12)     | `textDocument/definition`        |
| Find References (Shift+F12)| `textDocument/references`        |
| Rename (F2)                | `textDocument/rename`            |
| Format Document            | `textDocument/formatting`        |
| Signature Help             | `textDocument/signatureHelp`     |
| Semantic Tokens            | `textDocument/semanticTokens/full`|

## LSP ↔ Editor Synchronization

When a document opens or changes, we must notify the server:

```
Open:  textDocument/didOpen  (full text)
Change: textDocument/didChange (incremental or full)
Close:  textDocument/didClose
Save:   textDocument/didSave
```

The `TextDocument` model tracks version numbers for incremental sync:

```csharp
class TextDocument {
    int _version = 0;
    string _languageId;

    void OnTextChanged() {
        _version++;
        _lspSession?.SendNotification("textDocument/didChange", new {
            textDocument = new { uri = ToUri(Path), version = _version },
            contentChanges = new[] { new { text = Content } }
        });
    }
}
```

## Buffering & Debouncing

Don't send LSP requests on every keystroke:

- **Diagnostics**: debounce 300ms after last change
- **Completion**: send immediately on trigger char (`.`), debounce 100ms on plain typing
- **Hover**: debounce 500ms

```csharp
class LSPManager {
    CancellationTokenSource? _diagnosticsCts;

    async void OnDocumentChanged() {
        _diagnosticsCts?.Cancel();
        _diagnosticsCts = new CancellationTokenSource();
        try {
            await Task.Delay(300, _diagnosticsCts.Token);
            // send didChange notification now
        } catch (TaskCanceledException) {
            // another change came, debounced
        }
    }
}
```
