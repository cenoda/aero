using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aero.Services.Git;
using Xunit;

namespace Aero.Tests.Services.Git;

/// <summary>
/// Tests for GitWatcher debounce logic, start/stop, and dispose race safety.
/// </summary>
public class GitWatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gitDir;

    public GitWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aero-gitwatcher-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _gitDir = Path.Combine(_tempDir, ".git");
        Directory.CreateDirectory(_gitDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void Constructor_NullGitDir_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentException>(() => new GitWatcher(null!, () => { }));
    }

    [Fact]
    public void Constructor_EmptyGitDir_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GitWatcher("", () => { }));
    }

    [Fact]
    public void Constructor_NullCallback_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GitWatcher(_gitDir, null!));
    }

    [Fact]
    public void Constructor_NonExistentPath_DoesNotThrow()
    {
        // R3.5: Graceful degradation — non-existent path should not throw
        var watcher = new GitWatcher("/nonexistent/path/.git", () => { });
        Assert.False(watcher.IsWatching);
        watcher.Dispose();
    }

    [Fact]
    public void Constructor_ValidPath_IsWatchingIsTrue()
    {
        using var watcher = new GitWatcher(_gitDir, () => { });
        Assert.True(watcher.IsWatching);
    }

    [Fact]
    public void StartStop_DoesNotThrow()
    {
        for (int i = 0; i < 5; i++)
        {
            var watcher = new GitWatcher(_gitDir, () => { });
            Assert.True(watcher.IsWatching);
            watcher.Dispose();
            Assert.False(watcher.IsWatching);
        }
    }

    [Fact]
    public void Dispose_Idempotent_NoError()
    {
        var watcher = new GitWatcher(_gitDir, () => { });
        watcher.Dispose();
        watcher.Dispose();
    }

    [Fact]
    public async Task Dispose_WhileDebouncePending_CallbackNotInvoked()
    {
        int callCount = 0;
        var watcher = new GitWatcher(_gitDir, () => Interlocked.Increment(ref callCount));
        File.WriteAllText(Path.Combine(_gitDir, "HEAD"), "ref: refs/heads/main\n");
        await Task.Delay(200);
        watcher.Dispose();
        await Task.Delay(800);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void HEAD_Change_FiresCallback()
    {
        int callCount = 0;
        var reset = new ManualResetEventSlim(false);
        using var watcher = new GitWatcher(_gitDir, () =>
        {
            Interlocked.Increment(ref callCount);
            reset.Set();
        });
        Assert.True(watcher.IsWatching);
        File.WriteAllText(Path.Combine(_gitDir, "HEAD"), "ref: refs/heads/main\n");
        reset.Wait(2000);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Index_Change_FiresCallback()
    {
        int callCount = 0;
        var reset = new ManualResetEventSlim(false);
        using var watcher = new GitWatcher(_gitDir, () =>
        {
            Interlocked.Increment(ref callCount);
            reset.Set();
        });
        File.WriteAllText(Path.Combine(_gitDir, "index"), "dummy index content");
        reset.Wait(2000);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void COMMIT_EDITMSG_Change_FiresCallback()
    {
        int callCount = 0;
        var reset = new ManualResetEventSlim(false);
        using var watcher = new GitWatcher(_gitDir, () =>
        {
            Interlocked.Increment(ref callCount);
            reset.Set();
        });
        File.WriteAllText(Path.Combine(_gitDir, "COMMIT_EDITMSG"), "test commit");
        reset.Wait(2000);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task OtherFile_Change_DoesNotFireCallback()
    {
        int callCount = 0;
        using var watcher = new GitWatcher(_gitDir, () =>
        {
            Interlocked.Increment(ref callCount);
        });
        File.WriteAllText(Path.Combine(_gitDir, "config"), "[core]\n\trepositoryformatversion = 0");
        File.WriteAllText(Path.Combine(_gitDir, "description"), "Unnamed repository");
        await Task.Delay(1000);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task Debounce_MultipleRapidEvents_FiresOnce()
    {
        int callCount = 0;
        var reset = new ManualResetEventSlim(false);
        using var watcher = new GitWatcher(_gitDir, () =>
        {
            Interlocked.Increment(ref callCount);
            reset.Set();
        });
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_gitDir, "HEAD"), $"ref: refs/heads/branch{i}\n");
            await Task.Delay(50);
        }
        reset.Wait(2000);
        Assert.Equal(1, callCount);
    }
}
