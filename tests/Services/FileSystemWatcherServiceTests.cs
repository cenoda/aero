using System;
using System.IO;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Services;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// Integration tests for <see cref="FileSystemWatcherService"/>. Uses real
/// temporary directories and a small injected debounce window so the tests
/// stay fast while exercising the actual <see cref="FileSystemWatcher"/>.
/// </summary>
public class FileSystemWatcherServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly StubMessageBus _bus = new();

    public FileSystemWatcherServiceTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "aero-fsw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private FileSystemWatcherService CreateService(int debounceMs = 50) =>
        new(_bus, new IgnoreList(), debounceMs);

    [Fact]
    public async Task CreateFile_UnderWatchedDir_PublishesFolderChanged_AfterDebounce()
    {
        using var service = CreateService();
        service.Watch(_tempRoot);

        var tcs = new TaskCompletionSource<FolderChanged>();
        _bus.Subscribe<FolderChanged>(msg => tcs.TrySetResult(msg));

        var filePath = Path.Combine(_tempRoot, "newfile.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        var msg = await tcs.Task;
        Assert.Equal(_tempRoot, msg.Path);
    }

    [Fact]
    public async Task ChangeInsideIgnoredDirectory_DoesNotPublishFolderChanged()
    {
        using var service = CreateService();
        service.Watch(_tempRoot);

        var binDir = Path.Combine(_tempRoot, "bin");
        Directory.CreateDirectory(binDir);

        var received = false;
        _bus.Subscribe<FolderChanged>(_ => received = true);

        var filePath = Path.Combine(binDir, "app.dll");
        await File.WriteAllTextAsync(filePath, "data");

        await Task.Delay(150); // longer than debounce

        Assert.False(received);
    }

    [Fact]
    public async Task Watch_NewPath_StopsPreviousWatcher()
    {
        var otherDir = Path.Combine(
            Path.GetTempPath(),
            "aero-fsw-other-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(otherDir);

        try
        {
            using var service = CreateService();
            service.Watch(_tempRoot);
            service.Watch(otherDir);

            var receivedOld = false;
            _bus.Subscribe<FolderChanged>(_ => receivedOld = true);

            // Change in the old path should not trigger — previous watcher stopped.
            await File.WriteAllTextAsync(Path.Combine(_tempRoot, "old.txt"), "x");
            await Task.Delay(150);

            Assert.False(receivedOld);

            // Change in the new path should trigger.
            var tcs = new TaskCompletionSource<FolderChanged>();
            _bus.Subscribe<FolderChanged>(msg => tcs.TrySetResult(msg));
            await File.WriteAllTextAsync(Path.Combine(otherDir, "new.txt"), "y");

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.Same(tcs.Task, completed);
            await tcs.Task;
        }
        finally
        {
            Directory.Delete(otherDir, recursive: true);
        }
    }

    [Fact]
    public void Dispose_SetsIsWatchingFalse()
    {
        var service = CreateService();
        service.Watch(_tempRoot);
        Assert.True(service.IsWatching);

        service.Dispose();

        Assert.False(service.IsWatching);
    }
}
