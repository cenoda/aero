using Aero.Languages;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Concurrent;
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
        using var transport = InMemoryDuplex.CreatePair();
        using var server = new FakeLspPeer(transport.ServerStream, transport.ServerStream, textDocumentSync: 2);
        using var session = new LSPSession(transport.ClientStream, transport.ClientStream);

        var initialized = await session.InitializeAsync("csharp-ls", "file:///workspace", CancellationToken.None);

        Assert.False(initialized);
        Assert.False(session.SupportsFullDocumentSync);
        Assert.Equal("LSP disabled for csharp-ls: server does not support full document sync.", session.LastStatusMessage);
    }

    private sealed class FakeLspPeer : IDisposable
    {
        private readonly JsonRpc _rpc;

        public FakeLspPeer(Stream sendingStream, Stream receivingStream, int textDocumentSync = 1)
        {
            _rpc = new JsonRpc(sendingStream, receivingStream, new FakeLspServer(textDocumentSync, this));
            _rpc.StartListening();
        }

        public TaskCompletionSource<bool> InitializeReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> InitializedNotificationReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ShutdownReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ExitReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    private sealed class FakeLspServer
    {
        private readonly int _textDocumentSync;
        private readonly FakeLspPeer _owner;

        public FakeLspServer(int textDocumentSync, FakeLspPeer owner)
        {
            _textDocumentSync = textDocumentSync;
            _owner = owner;
        }

        [JsonRpcMethod("initialize")]
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

        [JsonRpcMethod("initialized")]
        public void Initialized(object @params)
        {
            _owner.InitializedNotificationReceived.TrySetResult(true);
        }

        [JsonRpcMethod("workspace/echo")]
        public EchoResponse Echo(EchoRequest request)
        {
            return new EchoResponse
            {
                Value = request.Value,
            };
        }

        [JsonRpcMethod("shutdown")]
        public object? Shutdown()
        {
            _owner.ShutdownReceived.TrySetResult(true);
            return null;
        }

        [JsonRpcMethod("exit")]
        public void Exit()
        {
            _owner.ExitReceived.TrySetResult(true);
        }
    }

    private sealed class InitializeResponse
    {
        public InitializeCapabilities Capabilities { get; set; } = new();
    }

    private sealed class InitializeCapabilities
    {
        public int TextDocumentSync { get; set; }
    }

    private sealed class EchoRequest
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class EchoResponse
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class InMemoryDuplex : IDisposable
    {
        private readonly ChannelStream _firstToSecond = new();
        private readonly ChannelStream _secondToFirst = new();

        private InMemoryDuplex()
        {
            ClientStream = new DuplexEndpointStream(_secondToFirst, _firstToSecond);
            ServerStream = new DuplexEndpointStream(_firstToSecond, _secondToFirst);
        }

        public Stream ClientStream { get; }

        public Stream ServerStream { get; }

        public static InMemoryDuplex CreatePair()
        {
            return new InMemoryDuplex();
        }

        public void Dispose()
        {
            ClientStream.Dispose();
            ServerStream.Dispose();
        }
    }

    private sealed class DuplexEndpointStream : Stream
    {
        private readonly ChannelStream _readStream;
        private readonly ChannelStream _writeStream;
        private bool _disposed;

        public DuplexEndpointStream(ChannelStream readStream, ChannelStream writeStream)
        {
            _readStream = readStream;
            _writeStream = writeStream;
        }

        public override bool CanRead => !_disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _readStream.ReadAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _writeStream.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (disposing)
            {
                _writeStream.Complete();
            }

            base.Dispose(disposing);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ChannelStream
    {
        private readonly ConcurrentQueue<byte[]> _segments = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly object _gate = new();
        private byte[]? _currentSegment;
        private int _currentOffset;
        private bool _isCompleted;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_gate)
                {
                    if (_currentSegment != null)
                    {
                        var remaining = _currentSegment.Length - _currentOffset;
                        var copyLength = Math.Min(buffer.Length, remaining);
                        _currentSegment.AsMemory(_currentOffset, copyLength).CopyTo(buffer);
                        _currentOffset += copyLength;

                        if (_currentOffset >= _currentSegment.Length)
                        {
                            _currentSegment = null;
                            _currentOffset = 0;
                        }

                        return copyLength;
                    }

                    if (_segments.TryDequeue(out var nextSegment))
                    {
                        _currentSegment = nextSegment;
                        _currentOffset = 0;
                        continue;
                    }

                    if (_isCompleted)
                    {
                        return 0;
                    }
                }

                await _signal.WaitAsync(cancellationToken);
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.Length == 0)
            {
                return ValueTask.CompletedTask;
            }

            _segments.Enqueue(buffer.ToArray());
            _signal.Release();
            return ValueTask.CompletedTask;
        }

        public void Complete()
        {
            lock (_gate)
            {
                _isCompleted = true;
            }

            _signal.Release();
        }
    }
}