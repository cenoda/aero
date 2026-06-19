using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Aero.Tests.Languages.Helpers;

internal sealed class FakeLspPeer : IDisposable
{
    private readonly JsonRpc _rpc;

    public FakeLspPeer(Stream sendingStream, Stream receivingStream, int textDocumentSync = 1)
    {
        var server = new FakeLspServer(textDocumentSync, this);
        _rpc = new JsonRpc(sendingStream, receivingStream, server);
        _rpc.StartListening();
    }

    public ConcurrentQueue<ReceivedLspMessage> ReceivedNotifications { get; } = new();

    public TaskCompletionSource<bool> InitializeReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> InitializedNotificationReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> ShutdownReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> ExitReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DidOpenReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DidChangeReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DidSaveReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DidCloseReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<ReceivedLspMessage> GetNotifications(string method)
    {
        return ReceivedNotifications.Where(n => n.Method == method).ToList();
    }

    public ReceivedLspMessage? GetLastNotification(string method)
    {
        return ReceivedNotifications.LastOrDefault(n => n.Method == method);
    }

    public void RecordNotification(string method, JToken? @params)
    {
        ReceivedNotifications.Enqueue(new ReceivedLspMessage(method, @params));
    }

    public Task SendLogMessageAsync(string message)
    {
        return _rpc.NotifyAsync(
            "window/logMessage",
            new
            {
                type = 3,
                message,
            });
    }

    public Task SendPublishDiagnosticsAsync(string uri)
    {
        return _rpc.NotifyAsync(
            "textDocument/publishDiagnostics",
            new
            {
                uri,
                diagnostics = new object[]
                {
                    new
                    {
                        message = "oops",
                        severity = 1,
                    },
                },
            });
    }

    public void Dispose()
    {
        _rpc.Dispose();
    }
}
