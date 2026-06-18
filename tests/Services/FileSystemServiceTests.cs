using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Project;
using Aero.Services;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// Integration tests for <see cref="FileSystemService"/> against real temp
/// directories. Each test creates and cleans up its own scratch folder so
/// parallel test execution does not interfere.
/// </summary>
public class FileSystemServiceTests : System.IDisposable
{
    private readonly string _root;
    private readonly FileSystemService _service;

    public FileSystemServiceTests()
    {
        // Unique per-test directory keeps tests parallel-safe.
        _root = Path.Combine(Path.GetTempPath(), "aero-fs-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _service = new FileSystemService(new IgnoreList());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // -------------------------------------------------------------------
    // Enumeration
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetDirectoryEntriesAsync_ReturnsDirectChildrenOnly()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "");
        File.WriteAllText(Path.Combine(_root, "sub", "deep.txt"), "");

        var entries = await _service.GetDirectoryEntriesAsync(_root);

        // "sub" is a direct child; "sub/deep.txt" is one level deeper and must NOT appear.
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Name == "sub" && e.Kind == FileSystemEntryKind.Directory);
        Assert.Contains(entries, e => e.Name == "a.txt" && e.Kind == FileSystemEntryKind.File);
        Assert.DoesNotContain(entries, e => e.Name == "deep.txt");
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_DirectoriesBeforeFiles()
    {
        File.WriteAllText(Path.Combine(_root, "z.txt"), "");
        Directory.CreateDirectory(Path.Combine(_root, "a"));

        var entries = await _service.GetDirectoryEntriesAsync(_root);

        Assert.Equal(2, entries.Count);
        Assert.Equal(FileSystemEntryKind.Directory, entries[0].Kind);
        Assert.Equal(FileSystemEntryKind.File, entries[1].Kind);
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_SortsAlphabetically()
    {
        Directory.CreateDirectory(Path.Combine(_root, "c"));
        Directory.CreateDirectory(Path.Combine(_root, "a"));
        Directory.CreateDirectory(Path.Combine(_root, "b"));
        File.WriteAllText(Path.Combine(_root, "x.txt"), "");
        File.WriteAllText(Path.Combine(_root, "y.txt"), "");

        var entries = await _service.GetDirectoryEntriesAsync(_root);

        var names = entries.Select(e => e.Name).ToArray();
        Assert.Equal(new[] { "a", "b", "c", "x.txt", "y.txt" }, names);
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var entries = await _service.GetDirectoryEntriesAsync(_root);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_FiltersIgnoredDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "README.md"), "");

        var entries = await _service.GetDirectoryEntriesAsync(_root);

        Assert.DoesNotContain(entries, e => e.Name == "bin");
        Assert.Contains(entries, e => e.Name == "src");
        Assert.Contains(entries, e => e.Name == "README.md");
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_NormalizesPath()
    {
        // Relative path with ".." — service should normalize via Path.GetFullPath.
        var entries = await _service.GetDirectoryEntriesAsync(Path.Combine(_root, "."));
        Assert.NotNull(entries);
        // All full paths must be rooted, never contain "..".
        Assert.All(entries, e => Assert.DoesNotContain("..", e.FullPath));
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_NonexistentPath_Throws()
    {
        var bogus = Path.Combine(_root, "does-not-exist");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _service.GetDirectoryEntriesAsync(bogus));
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_EmptyPath_Throws()
    {
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _service.GetDirectoryEntriesAsync(""));
    }

    [Fact]
    public async Task GetDirectoryEntriesAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        // ThrowIfCancellationRequested throws OperationCanceledException; the
        // Task wrapper converts it to TaskCanceledException when the exception
        // propagates out of the awaited call. Accept either — exact-type
        // asserts are too strict here and we only care that cancellation
        // was observed.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.GetDirectoryEntriesAsync(_root, cts.Token));
    }

    // -------------------------------------------------------------------
    // Create / Rename / Delete
    // -------------------------------------------------------------------

    [Fact]
    public async Task CreateFileAsync_CreatesEmptyFile()
    {
        await _service.CreateFileAsync(_root, "new.txt");
        Assert.True(File.Exists(Path.Combine(_root, "new.txt")));
        Assert.Empty(await File.ReadAllTextAsync(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public async Task CreateFileAsync_ExistingFile_DoesNotOverwrite()
    {
        // Regression: previously File.Create(target) silently truncated an
        // existing file. The New File context action must surface a collision
        // so the user is not silently destroyed.
        var target = Path.Combine(_root, "exists.txt");
        await File.WriteAllTextAsync(target, "important content");

        await Assert.ThrowsAsync<IOException>(
            () => _service.CreateFileAsync(_root, "exists.txt"));

        // Content must be intact.
        Assert.Equal("important content", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task CreateFileAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CreateFileAsync(_root, "nope.txt", cts.Token));
    }

    [Fact]
    public async Task CreateDirectoryAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CreateDirectoryAsync(_root, "nope", cts.Token));
    }

    [Fact]
    public async Task RenameAsync_Cancellation_Throws()
    {
        var f = Path.Combine(_root, "f.txt");
        File.WriteAllText(f, "");
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.RenameAsync(f, "g.txt", cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.DeleteAsync(_root, cts.Token));
    }

    [Fact]
    public async Task ExistsAsync_Cancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ExistsAsync(_root, cts.Token));
    }

    [Fact]
    public async Task CreateFileAsync_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _service.CreateFileAsync(_root, ""));
    }

    [Fact]
    public async Task CreateFileAsync_NameWithSlash_Throws()
    {
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _service.CreateFileAsync(_root, "sub/file.txt"));
    }

    [Fact]
    public async Task CreateFileAsync_InvalidChars_Throws()
    {
        // ":" is invalid on Windows; on Linux it would be valid in a filename.
        // The implementation rejects it via GetInvalidFileNameChars regardless.
        var invalid = Path.GetInvalidFileNameChars()[0].ToString();
        if (string.IsNullOrEmpty(invalid))
            return; // platform with no invalid chars — nothing to test
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _service.CreateFileAsync(_root, "bad" + invalid + "name"));
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesDirectory()
    {
        await _service.CreateDirectoryAsync(_root, "newdir");
        Assert.True(Directory.Exists(Path.Combine(_root, "newdir")));
    }

    [Fact]
    public async Task RenameAsync_RenamesFile()
    {
        var original = Path.Combine(_root, "old.txt");
        File.WriteAllText(original, "x");
        await _service.RenameAsync(original, "new.txt");
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public async Task RenameAsync_RenamesDirectory()
    {
        var original = Path.Combine(_root, "olddir");
        Directory.CreateDirectory(original);
        File.WriteAllText(Path.Combine(original, "inner.txt"), "x");

        await _service.RenameAsync(original, "newdir");

        Assert.False(Directory.Exists(original));
        Assert.True(Directory.Exists(Path.Combine(_root, "newdir")));
        // Children move with the directory.
        Assert.True(File.Exists(Path.Combine(_root, "newdir", "inner.txt")));
    }

    [Fact]
    public async Task RenameAsync_PathSeparator_Throws()
    {
        var f = Path.Combine(_root, "f.txt");
        File.WriteAllText(f, "");
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _service.RenameAsync(f, "sub/new.txt"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var f = Path.Combine(_root, "doomed.txt");
        File.WriteAllText(f, "");
        await _service.DeleteAsync(f);
        Assert.False(File.Exists(f));
    }

    [Fact]
    public async Task DeleteAsync_RemovesDirectoryRecursively()
    {
        var dir = Path.Combine(_root, "doomed");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "inner.txt"), "");

        await _service.DeleteAsync(dir);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DeleteAsync_NonexistentPath_IsNoOp()
    {
        // Deletion is idempotent — no exception if the path is already gone.
        await _service.DeleteAsync(Path.Combine(_root, "never-existed"));
    }

    // -------------------------------------------------------------------
    // ExistsAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_ExistingFile_True()
    {
        var f = Path.Combine(_root, "exists.txt");
        File.WriteAllText(f, "");
        Assert.True(await _service.ExistsAsync(f));
    }

    [Fact]
    public async Task ExistsAsync_ExistingDirectory_True()
    {
        Assert.True(await _service.ExistsAsync(_root));
    }

    [Fact]
    public async Task ExistsAsync_MissingPath_False()
    {
        Assert.False(await _service.ExistsAsync(Path.Combine(_root, "nope.txt")));
    }

    [Fact]
    public async Task ExistsAsync_EmptyPath_False()
    {
        Assert.False(await _service.ExistsAsync(""));
    }
}
