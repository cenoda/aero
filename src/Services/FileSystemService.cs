using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Project;

namespace Aero.Services;

/// <summary>
/// Default <see cref="IFileSystemService"/> backed by <see cref="System.IO"/>.
/// All paths are normalized via <see cref="Path.GetFullPath(string)"/> so the
/// caller always sees canonical paths regardless of input (<c>"."</c>,
/// trailing separators, etc.).
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly IIgnoreList _ignoreList;

    public FileSystemService(IIgnoreList ignoreList)
    {
        _ignoreList = ignoreList ?? throw new ArgumentNullException(nameof(ignoreList));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileSystemEntry>> GetDirectoryEntriesAsync(
        string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        // Check cancellation up front. Without this, an empty directory would
        // skip the loop body and never observe a pre-cancelled token.
        ct.ThrowIfCancellationRequested();

        var normalized = NormalizePath(path);

        // Directory.EnumerateDirectories / EnumerateFiles does not accept a
        // CancellationToken, so we materialize the enumerators and check the
        // token between yields. For very large directories this lets us bail
        // out before completing enumeration.
        var dirEnum = Directory.EnumerateDirectories(normalized).GetEnumerator();
        var fileEnum = Directory.EnumerateFiles(normalized).GetEnumerator();

        var dirs = new List<FileSystemEntry>();
        var files = new List<FileSystemEntry>();

        try
        {
            while (dirEnum.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                var dir = dirEnum.Current;
                if (_ignoreList.IsIgnored(dir, isDirectory: true))
                    continue;
                dirs.Add(ToEntry(dir, FileSystemEntryKind.Directory));
            }
            while (fileEnum.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                var file = fileEnum.Current;
                if (_ignoreList.IsIgnored(file, isDirectory: false))
                    continue;
                files.Add(ToEntry(file, FileSystemEntryKind.File));
            }
        }
        finally
        {
            dirEnum.Dispose();
            fileEnum.Dispose();
        }

        // Use Task.Yield once so the method is genuinely async on the hot path.
        // For huge directories the enumerator above already yields via MoveNext,
        // but a single Task.Yield keeps the contract honest.
        await Task.Yield();

        return dirs
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Concat(files.OrderBy(e => e.Name, StringComparer.Ordinal))
            .ToList();
    }

    /// <inheritdoc />
    public Task CreateFileAsync(string parentPath, string name, CancellationToken ct = default)
    {
        ValidateName(name);
        var target = ResolveChild(parentPath, name);
        // File.Create returns a stream we immediately dispose — guarantees the
        // file exists and is empty on disk before we return.
        using var _ = File.Create(target);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default)
    {
        ValidateName(name);
        var target = ResolveChild(parentPath, name);
        Directory.CreateDirectory(target);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenameAsync(string path, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        ValidateName(newName);

        var normalized = NormalizePath(path);
        var parent = Path.GetDirectoryName(normalized)
            ?? throw new InvalidOperationException("Cannot rename a root path.");
        var target = Path.Combine(parent, newName);

        if (Directory.Exists(normalized))
            Directory.Move(normalized, target);
        else if (File.Exists(normalized))
            File.Move(normalized, target);
        else
            throw new FileNotFoundException("Path not found.", normalized);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        var normalized = NormalizePath(path);

        if (Directory.Exists(normalized))
            Directory.Delete(normalized, recursive: true);
        else if (File.Exists(normalized))
            File.Delete(normalized);
        // If the path is already gone, treat it as a no-op rather than an error —
        // deletion is idempotent from the caller's perspective.

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            return Task.FromResult(false);

        var normalized = NormalizePath(path);
        return Task.FromResult(Directory.Exists(normalized) || File.Exists(normalized));
    }

    // --- helpers ---------------------------------------------------------

    private static FileSystemEntry ToEntry(string fullPath, FileSystemEntryKind kind) =>
        new(Path.GetFileName(fullPath), fullPath, kind);

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static string ResolveChild(string parentPath, string name)
    {
        var parent = NormalizePath(parentPath);
        return Path.Combine(parent, name);
    }

    /// <summary>
    /// Reject names that would let a caller escape the parent directory.
    /// Empty / whitespace / path separators / invalid filename chars → throw.
    /// </summary>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));
        if (name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException("Name must not contain path separators.", nameof(name));
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Name contains invalid file name characters.", nameof(name));
    }
}
