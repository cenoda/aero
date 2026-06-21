using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Languages;

public sealed class LSPSession : IDisposable
{
    private const int FullDocumentSyncKind = 1;
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(2);

    private readonly JsonRpc _jsonRpc;
    private readonly Process? _process;
    private readonly IDisposable? _ownedTransport;
    private readonly Action<string>? _statusSink;
    private readonly object _disposeLock = new();
    private bool _isDisposed;

    public LSPSession(Stream sendingStream, Stream receivingStream, Action<string>? statusSink = null)
    {
        _ = sendingStream ?? throw new ArgumentNullException(nameof(sendingStream));
        _ = receivingStream ?? throw new ArgumentNullException(nameof(receivingStream));

        _statusSink = statusSink;
        var formatter = new JsonMessageFormatter();
        var handler = new HeaderDelimitedMessageHandler(sendingStream, receivingStream, formatter);
        _ownedTransport = handler;
        _jsonRpc = new JsonRpc(handler);
        _jsonRpc.AddLocalRpcTarget(new RpcNotificationTarget(this));
        _jsonRpc.StartListening();
    }

    private LSPSession(Process process, Stream sendingStream, Stream receivingStream, Action<string>? statusSink)
        : this(sendingStream, receivingStream, statusSink)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public event EventHandler<LSPDiagnosticsEventArgs>? PublishDiagnosticsReceived;

    public event EventHandler<LSPLogMessageEventArgs>? LogMessageReceived;

    public bool SupportsFullDocumentSync { get; private set; }

    public string? LastStatusMessage { get; private set; }

    public async Task<bool> InitializeAsync(string serverName, string? rootUri, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (string.IsNullOrWhiteSpace(serverName))
        {
            throw new ArgumentException("Server name is required.", nameof(serverName));
        }

        var result = await SendRequestAsync<InitializeResult>(
            "initialize",
            new
            {
                rootUri,
                capabilities = new
                {
                    textDocument = new
                    {
                    },
                    workspace = new
                    {
                    },
                },
            },
            cancellationToken).ConfigureAwait(false);

        SupportsFullDocumentSync = IsFullDocumentSyncSupported(result?.Capabilities?.TextDocumentSync);

        if (!SupportsFullDocumentSync)
        {
            SetStatus($"LSP disabled for {serverName}: server does not support full document sync.");
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _jsonRpc.NotifyAsync("initialized", new { }).ConfigureAwait(false);
        SetStatus($"LSP initialized: {serverName}");
        return true;
    }

    public async Task<T> SendRequestAsync<T>(string method, object? @params, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method is required.", nameof(method));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _jsonRpc.InvokeAsync<T>(method, @params).ConfigureAwait(false);
    }

    /// <summary>
    /// Request textDocument/completion for the given document and caret position.
    /// </summary>
    public async Task<IList<CompletionItem>> RequestCompletionAsync(
        string textDocumentUri,
        int line,
        int character,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _jsonRpc.InvokeAsync<IList<CompletionItem>>(
            "textDocument/completion",
            new
            {
                textDocument = new { uri = textDocumentUri },
                position = new { line, character }
            },
            cancellationToken).ConfigureAwait(false);

        return result ?? new List<CompletionItem>();
    }

    public void SendNotification(string method, object? @params)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method is required.", nameof(method));
        }

        _ = _jsonRpc.NotifyAsync(method, @params);
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _jsonRpc.InvokeAsync<object?>("shutdown").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ConnectionLostException or ObjectDisposedException or InvalidOperationException)
        {
            SetStatus($"LSP shutdown request skipped: {ex.Message}");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _jsonRpc.NotifyAsync("exit").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ConnectionLostException or ObjectDisposedException or InvalidOperationException)
        {
            SetStatus($"LSP exit notification skipped: {ex.Message}");
        }
    }

    public static LSPSession StartProcess(string fileName, string? arguments, Action<string>? statusSink = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Process file name is required.", nameof(fileName));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                statusSink?.Invoke($"LSP stderr: {e.Data}");
            }
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start LSP server '{fileName}'.");
        }

        process.BeginErrorReadLine();

        return new LSPSession(
            process,
            process.StandardInput.BaseStream,
            process.StandardOutput.BaseStream,
            statusSink);
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        try
        {
            _jsonRpc.Dispose();
        }
        catch
        {
            // Best-effort teardown.
        }

        _ownedTransport?.Dispose();

        if (_process == null)
        {
            return;
        }

        try
        {
            if (_process.HasExited)
            {
                _process.Dispose();
                return;
            }

            if (!_process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds))
            {
                _process.Kill(true);
                _process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            SetStatus($"LSP process cleanup warning: {ex.Message}");
        }
        finally
        {
            _process.Dispose();
        }
    }

    private void OnPublishDiagnostics(PublishDiagnosticsParams @params)
    {
        PublishDiagnosticsReceived?.Invoke(this, new LSPDiagnosticsEventArgs(@params));
    }

    private void OnLogMessage(LogMessageParams @params)
    {
        LogMessageReceived?.Invoke(this, new LSPLogMessageEventArgs(@params));
    }

    private void SetStatus(string message)
    {
        LastStatusMessage = message;
        _statusSink?.Invoke(message);
    }

private static bool IsFullDocumentSyncSupported(JToken? textDocumentSync)
    {
        if (textDocumentSync is null)
        {
            return false;
        }

        int syncKind = -1;

        if (textDocumentSync.Type == JTokenType.Integer)
        {
            syncKind = textDocumentSync.Value<int>();
        }
        else if (textDocumentSync.Type == JTokenType.Object)
        {
            var changeToken = textDocumentSync["change"];
            if (changeToken?.Type == JTokenType.Integer)
            {
                syncKind = changeToken.Value<int>();
            }
        }

        // Accept both full (1) and incremental (2). Phase 4 will always send full
        // regardless of what the server advertises per Plan §5.1.
        return syncKind == FullDocumentSyncKind || syncKind == 2;
    }

    private sealed class RpcNotificationTarget
    {
        private readonly LSPSession _owner;

        public RpcNotificationTarget(LSPSession owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        [JsonRpcMethod("textDocument/publishDiagnostics")]
        public void PublishDiagnostics(PublishDiagnosticsParams @params)
        {
            _owner.OnPublishDiagnostics(@params);
        }

        [JsonRpcMethod("window/logMessage")]
        public void LogMessage(LogMessageParams @params)
        {
            _owner.OnLogMessage(@params);
        }

        [JsonRpcMethod("$/logTrace")]
        public void LogTrace(LogMessageParams @params)
        {
            _owner.OnLogMessage(@params);
        }
    }
}