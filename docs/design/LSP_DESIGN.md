# Language Server Protocol (LSP) Design

> **Note**: This document describes the LSP design. For abstraction-first principles, see `AGENTS.md` Section 4.

## Abstraction-First (ILSPService)

Aero uses abstraction-first design for multi-language support:

```csharp
public interface ILSPService
{
    // Language identification
    string Language { get; }  // "csharp", "python", "rust", "typescript"
    string Name { get; }    // "csharp-ls", "pylsp", "rust-analyzer"
    
    // Lifecycle
    Task InitializeAsync(string workspaceRoot, CancellationToken ct);
    Task ShutdownAsync(CancellationToken ct);
    
    // Document operations
    Task OpenDocumentAsync(string filePath, string content, CancellationToken ct);
    Task ChangeDocumentAsync(string filePath, string content, CancellationToken ct);
    Task CloseDocumentAsync(string filePath, CancellationToken ct);
    
    // Language features
    Task<IEnumerable<CompletionItem>> GetCompletionsAsync(string filePath, Position pos);
    Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(string filePath);
    Task<GotoResult?> GoToDefinitionAsync(string filePath, Position pos);
    Task<RenameResult> RenameSymbolAsync(string filePath, Position pos, string newName);
    Task<string?> GetHoverAsync(string filePath, Position pos);
}

public record Position(int Line, int Column);
public record CompletionItem(string Label, string? InsertText, CompletionKind Kind);
public record Diagnostic(Range Range, string Message, DiagnosticSeverity Severity);
public record GotoResult(Uri Location, Range Range);
public record RenameResult(WorkspaceEdit? DocumentChanges);
```

### Implementations

```
ILSPService (interface)
    │
    ├── CSharpLSPService    ← Phase 4: csharp-ls
    ├── PythonLSPService   ← Future: pylsp
    ├── RustLSPService     ← Future: rust-analyzer
    └── TypeScriptService  ← Future: ts_ls
```

### Factory

```csharp
public class LSPServiceFactory
{
    public ILSPService? Detect(string workspacePath)
    {
        // Check for *.csproj → CSharpLSPService
        // Check for *.py / pyproject.toml → PythonLSPService
        // Check for Cargo.toml → RustLSPService
        // Check for package.json → TypeScriptService
    }
}
```

This allows adding new languages without rewriting core logic.

## Architecture

```
User types in editor
    → LSPManager routes document lifecycle and completion requests
    → LSPSession sends JSON-RPC over stdin/stdout
    → Language Server process responds
    → LSPSession deserializes response
    → LSPManager / DiagnosticStore updates ViewModels (diagnostics, completions, hover)
```

## Phase 4 Constraints

This design doc describes the full LSP surface. The current implementation phase
([`docs/phases/phase-4/IMPLEMENTATION_PLAN.md`](../phases/phase-4/IMPLEMENTATION_PLAN.md))
is intentionally narrower:

- **Single server:** `csharp-ls` is the only language server integrated in Phase 4.
- **Single active root:** one LSP session per opened folder; opening a different folder
  closes the previous session.
- **Full-document sync only:** `textDocument/didChange` sends the entire document content.
  Incremental sync is deferred to a later phase.
- **Document identity:** `DocumentOpened` is published with a file path string; `LSPManager`
  resolves the `TextDocument` through `DocumentManager`.
- **Diagnostic rendering:** AvaloniaEdit 11.3 does not ship `TextMarkerService`. Phase 4
  uses `AvaloniaEdit.Rendering.IBackgroundRenderer` for editor-visible diagnostics.
- **Scope:** diagnostics, Problems panel, and `Ctrl+Space` completion. Hover, go-to-definition,
  rename, formatting, signature help, and semantic tokens are out of scope.

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
Open:   textDocument/didOpen  (full text)
Change: textDocument/didChange (full text in Phase 4)
Close:  textDocument/didClose
Save:   textDocument/didSave
```

The `TextDocument` model tracks version numbers for buffer sync:

```csharp
class TextDocument {
    int _version = 0;
    string _languageId;

    void OnTextChanged() {
        // Phase 4: version is incremented only when a debounced change is actually sent,
        // not on every keystroke, so the server's view of the version never races ahead.
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
