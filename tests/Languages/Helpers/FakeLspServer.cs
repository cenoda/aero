using System;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Aero.Tests.Languages.Helpers;

internal sealed class FakeLspServer
{
    private readonly int _textDocumentSync;
    private readonly FakeLspPeer _owner;

    public FakeLspServer(int textDocumentSync, FakeLspPeer owner)
    {
        _textDocumentSync = textDocumentSync;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    [JsonRpcMethod(LspMessageNames.Initialize)]
    public InitializeResponse Initialize(object @params)
    {
        _owner.InitializeReceived.TrySetResult(true);
        return new InitializeResponse
        {
            Capabilities = new InitializeCapabilities
            {
                TextDocumentSync = _textDocumentSync,
            },
        };
    }

    [JsonRpcMethod(LspMessageNames.Initialized)]
    public void Initialized(object @params)
    {
        _owner.InitializedNotificationReceived.TrySetResult(true);
    }

    [JsonRpcMethod(LspMessageNames.Shutdown)]
    public object? Shutdown()
    {
        _owner.ShutdownReceived.TrySetResult(true);
        return null;
    }

    [JsonRpcMethod(LspMessageNames.Exit)]
    public void Exit()
    {
        _owner.ExitReceived.TrySetResult(true);
    }

    [JsonRpcMethod(LspMessageNames.DidOpen)]
    public void DidOpen(JObject @params)
    {
        _owner.RecordNotification(LspMessageNames.DidOpen, @params);
        _owner.DidOpenReceived.TrySetResult(true);
    }

    [JsonRpcMethod(LspMessageNames.DidChange)]
    public void DidChange(JObject @params)
    {
        _owner.RecordNotification(LspMessageNames.DidChange, @params);
        _owner.DidChangeReceived.TrySetResult(true);
    }

    [JsonRpcMethod(LspMessageNames.DidSave)]
    public void DidSave(JObject @params)
    {
        _owner.RecordNotification(LspMessageNames.DidSave, @params);
        _owner.DidSaveReceived.TrySetResult(true);
    }

    [JsonRpcMethod(LspMessageNames.DidClose)]
    public void DidClose(JObject @params)
    {
        _owner.RecordNotification(LspMessageNames.DidClose, @params);
        _owner.DidCloseReceived.TrySetResult(true);
    }

    [JsonRpcMethod("workspace/echo")]
    public EchoResponse Echo(EchoRequest request)
    {
        return new EchoResponse
        {
            Value = request.Value,
        };
    }
}

internal sealed class InitializeResponse
{
    public InitializeCapabilities Capabilities { get; set; } = new();
}

internal sealed class InitializeCapabilities
{
    public int TextDocumentSync { get; set; }
}

internal sealed class EchoRequest
{
    public string Value { get; set; } = string.Empty;
}

internal sealed class EchoResponse
{
    public string Value { get; set; } = string.Empty;
}
