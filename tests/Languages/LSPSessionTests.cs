using Aero.Languages;
using Aero.Tests.Languages.Helpers;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Aero.Tests.Languages;

public class LSPSessionTests
{
    [Fact]
    public async Task InitializeAsync_SendsHandshake_AndAcceptsFullSync()
    {
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);

        Assert.True(initialized);
        Assert.True(session.SupportsFullDocumentSync);
        Assert.True(await server.InitializeReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(await server.InitializedNotificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("LSP initialized: csharp-ls", session.LastStatusMessage);
    }

    [Fact]
    public async Task SendRequestAsync_CorrelatesResponse()
    {
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);
        Assert.True(initialized);

        var response = await session.SendRequestAsync<EchoResponse>(
            "workspace/echo",
            new EchoRequest { Value = "ping" },
            CancellationToken.None);

        Assert.Equal("ping", response.Value);
    }

    [Fact]
    public async Task LogMessageNotification_RaisesEvent()
    {
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var eventReceived = new TaskCompletionSource<LogMessageParams>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.LogMessageReceived += (_, args) => eventReceived.TrySetResult(args.Message);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);
        Assert.True(initialized);

        await server.SendLogMessageAsync("ready");

        var logMessage = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("ready", logMessage.Message);
    }

    [Fact]
    public async Task PublishDiagnosticsNotification_RaisesEvent_WithReadablePayload()
    {
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var eventReceived = new TaskCompletionSource<PublishDiagnosticsParams>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.PublishDiagnosticsReceived += (_, args) => eventReceived.TrySetResult(args.Diagnostics);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);
        Assert.True(initialized);

        await server.SendPublishDiagnosticsAsync("file:///test.cs");

        var diagnostics = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("file:///test.cs", diagnostics.Uri);
        Assert.Equal(JTokenType.Array, diagnostics.Diagnostics.Type);
        Assert.Equal("oops", diagnostics.Diagnostics[0]?["message"]?.Value<string>());
    }

    [Fact]
    public async Task ShutdownAsync_SendsShutdownAndExit()
    {
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);
        Assert.True(initialized);

        await session.ShutdownAsync(CancellationToken.None);

        Assert.True(await server.ShutdownReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(await server.ExitReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

[Fact]
    public async Task InitializeAsync_NonFullSync_FailsGracefully()
    {
        // R8.2: We now accept both full (1) and incremental (2) sync.
        // This test verifies that incremental (2) is accepted.
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream, textDocumentSync: 2);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);

        // Now we accept incremental (2) as valid per R8.2
        Assert.True(initialized);
        Assert.True(session.SupportsFullDocumentSync);
        Assert.Equal("LSP initialized: csharp-ls", session.LastStatusMessage);
    }
}
