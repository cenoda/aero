using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Editor;
using Aero.Services;
using Aero.Tests.Languages.Helpers;
using Aero.Tests.Stubs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aero.Tests.Languages;

public class LSPManagerTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void DocumentOpened_CSharpFile_SendsDidOpen()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".cs", "class A { }");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            var doc = await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];
            Assert.True(await peer.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var notification = peer.GetLastNotification(LspMessageNames.DidOpen);
            Assert.NotNull(notification);
            var textDoc = notification.Params?["textDocument"];
            Assert.Equal(doc.Uri, textDoc?["uri"]?.Value<string>());
            Assert.Equal("csharp", textDoc?["languageId"]?.Value<string>());
            Assert.Equal(0, textDoc?["version"]?.Value<int>());
            Assert.Equal("class A { }", textDoc?["text"]?.Value<string>());
        });
    }

    [Fact]
    public void DocumentTextChanged_SendsDebouncedFullDocumentDidChange_WithIncrementedVersion()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager(debounce: TimeSpan.FromMilliseconds(100));
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".cs", "class A { }");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            var doc = await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];
            Assert.True(await peer.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            doc.Content = "class B { }";
            bus.Publish(new DocumentTextChanged(doc));

            // Wait less than the debounce window, then publish a second change.
            await Task.Delay(TimeSpan.FromMilliseconds(30));
            doc.Content = "class C { }";
            bus.Publish(new DocumentTextChanged(doc));

            Assert.True(await peer.DidChangeReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var notifications = peer.GetNotifications(LspMessageNames.DidChange);
            Assert.Single(notifications);

            var textDoc = notifications[0].Params?["textDocument"];
            Assert.Equal(doc.Uri, textDoc?["uri"]?.Value<string>());
            Assert.Equal(1, textDoc?["version"]?.Value<int>());

            var changes = notifications[0].Params?["contentChanges"] as JArray;
            Assert.NotNull(changes);
            Assert.Single(changes);
            Assert.Equal("class C { }", changes[0]?["text"]?.Value<string>());
        });
    }

    [Fact]
    public void DocumentSaved_SendsDidSave()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".cs", "class A { }");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            var doc = await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];
            Assert.True(await peer.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            await documentManager.SaveDocumentAsync(doc);
            Assert.True(await peer.DidSaveReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var notification = peer.GetLastNotification(LspMessageNames.DidSave);
            Assert.NotNull(notification);
            Assert.Equal(doc.Uri, notification.Params?["textDocument"]?["uri"]?.Value<string>());
        });
    }

    [Fact]
    public void DocumentClosed_SendsDidClose()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".cs", "class A { }");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            var doc = await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];
            Assert.True(await peer.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            documentManager.CloseDocument(doc);
            Assert.True(await peer.DidCloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var notification = peer.GetLastNotification(LspMessageNames.DidClose);
            Assert.NotNull(notification);
            Assert.Equal(doc.Uri, notification.Params?["textDocument"]?["uri"]?.Value<string>());
        });
    }

    [Fact]
    public void BufferEvents_FlowInCorrectOrder()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".cs", "class A { }");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            var doc = await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];
            Assert.True(await peer.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            doc.Content = "class B { }";
            bus.Publish(new DocumentTextChanged(doc));
            await Task.Delay(TimeSpan.FromMilliseconds(30));

            await documentManager.SaveDocumentAsync(doc);
            Assert.True(await peer.DidSaveReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            documentManager.CloseDocument(doc);
            Assert.True(await peer.DidCloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var methods = peer.ReceivedNotifications.Select(n => n.Method).ToList();
            Assert.Equal(new[] { LspMessageNames.DidOpen, LspMessageNames.DidChange, LspMessageNames.DidSave, LspMessageNames.DidClose }, methods);
        });
    }

    [Fact]
    public void NonCsDocument_IsNotSynced()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folder = CreateTempDirectory();
            var path = CreateTempFile(folder, ".txt", "hello");

            bus.Publish(new FolderOpened(folder));
            await WaitForInitializationAsync(factory);

            await documentManager.OpenDocumentAsync(path);
            var peer = factory.Peers[0];

            // Give any misrouted notification a moment to arrive.
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            Assert.Empty(peer.GetNotifications(LspMessageNames.DidOpen));
        });
    }

    [Fact]
    public void NoFolderOpen_CSharpFile_IsNotSyncedAndDoesNotCrash()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var path = CreateTempFile(CreateTempDirectory(), ".cs", "class A { }");

            var doc = await documentManager.OpenDocumentAsync(path);
            doc.Content = "class B { }";
            bus.Publish(new DocumentTextChanged(doc));
            await documentManager.SaveDocumentAsync(doc);
            documentManager.CloseDocument(doc);

            Assert.Empty(factory.Peers);
        });
    }

    [Fact]
    public void FactoryThrows_IsHandledGracefully_WithStatusMessage()
    {
        var bus = new StubMessageBus();
        var languageDetection = new LanguageDetectionService();
        var documentManager = new DocumentManager(bus, languageDetection);
        using var manager = new LSPManager(
            bus,
            documentManager,
            languageDetection,
            (_, _) => throw new InvalidOperationException("csharp-ls not found"),
            TimeSpan.Zero);

        bus.Publish(new FolderOpened(CreateTempDirectory()));

        var status = bus.MessagesOf<StatusMessage>().LastOrDefault();
        Assert.NotNull(status);
        Assert.Contains("csharp-ls not found", status.Text);
    }

    [Fact]
    public void OpenDifferentFolder_DisposesPreviousSession()
    {
        SingleThread.Run(async () =>
        {
            var (manager, bus, documentManager, factory) = CreateManager();
            using var _ = manager;

            var folderA = CreateTempDirectory();
            var folderB = CreateTempDirectory();

            bus.Publish(new FolderOpened(folderA));
            await WaitForInitializationAsync(factory);

            var pathA = CreateTempFile(folderA, ".cs", "class A { }");
            await documentManager.OpenDocumentAsync(pathA);
            var peerA = factory.Peers[0];
            Assert.True(await peerA.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var firstSession = factory.Sessions[0];

            bus.Publish(new FolderOpened(folderB));
            await WaitForInitializationAsync(factory, sessionIndex: 1);

            // The previous session should be disposed and unusable.
            Assert.Throws<ObjectDisposedException>(() => firstSession.SendNotification("foo", new { }));

            var pathB = CreateTempFile(folderB, ".cs", "class B { }");
            await documentManager.OpenDocumentAsync(pathB);
            var peerB = factory.Peers[1];
            Assert.True(await peerB.DidOpenReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            // No new notifications should reach the old peer.
            Assert.Single(peerA.GetNotifications(LspMessageNames.DidOpen));
            Assert.Single(peerB.GetNotifications(LspMessageNames.DidOpen));
        });
    }

    private static (LSPManager Manager, StubMessageBus Bus, DocumentManager DocumentManager, FakeSessionFactory Factory) CreateManager(
        TimeSpan? debounce = null)
    {
        var bus = new StubMessageBus();
        var languageDetection = new LanguageDetectionService();
        var documentManager = new DocumentManager(bus, languageDetection);
        var factory = new FakeSessionFactory();
        var manager = new LSPManager(
            bus,
            documentManager,
            languageDetection,
            factory.Create,
            debounce ?? TimeSpan.Zero);
        return (manager, bus, documentManager, factory);
    }

    private static async Task WaitForInitializationAsync(FakeSessionFactory factory, int sessionIndex = 0)
    {
        var peer = factory.Peers[sessionIndex];
        Assert.True(await peer.InitializeReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(await peer.InitializedNotificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private string CreateTempFile(string directory, string extension, string content)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private sealed class FakeSessionFactory : IDisposable
    {
        private readonly List<InMemoryDuplex> _transports = new();
        private readonly List<FakeLspPeer> _peers = new();
        private readonly List<LSPSession> _sessions = new();
        private bool _disposed;

        public IReadOnlyList<FakeLspPeer> Peers => _peers;

        public IReadOnlyList<LSPSession> Sessions => _sessions;

        public LSPSession Create(string serverName, string? rootUri)
        {
            var transport = InMemoryDuplex.CreatePair();
            var peer = new FakeLspPeer(transport.ServerStream, transport.ServerStream);
            var session = new LSPSession(transport.ClientStream, transport.ClientStream);
            _transports.Add(transport);
            _peers.Add(peer);
            _sessions.Add(session);
            return session;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var session in _sessions)
                session.Dispose();
            foreach (var peer in _peers)
                peer.Dispose();
            foreach (var transport in _transports)
                transport.Dispose();
        }
    }
}
