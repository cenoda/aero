using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Project;
using Aero.Services;

namespace Aero.Tests.Stubs;

/// <summary>
/// In-memory <see cref="IFileSystemService"/> for ViewModel tests. Stores a
/// dictionary of <c>path → entry</c>; path lookups are case-sensitive (matches
/// how tests assert on what they put in). Operations that would touch the
/// real disk complete synchronously against this map.
///
/// Designed for tree-building tests, not for stress-testing the service. Use
/// <see cref="Aero.Tests.Services.FileSystemServiceTests"/> for that.
/// </summary>
public sealed class MockFileSystemService : IFileSystemService
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IIgnoreList _ignoreList;

    private enum EntryKind { File, Directory }
    private sealed record Entry(string Name, EntryKind Kind);

    public MockFileSystemService(IIgnoreList? ignoreList = null)
    {
        _ignoreList = ignoreList ?? new IgnoreList(new string[0]); // no-op list by default
    }

    /// <summary>Helper: register a file at the given path.</summary>
    public void AddFile(string path)
    {
        var (full, name) = Normalize(path);
        _entries[full] = new Entry(name, EntryKind.File);
    }

    /// <summary>Helper: register a directory at the given path.</summary>
    public void AddDirectory(string path)
    {
        var (full, name) = Normalize(path);
        _entries[full] = new Entry(name, EntryKind.Directory);
    }

    public Task<IReadOnlyList<FileSystemEntry>> GetDirectoryEntriesAsync(
        string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (normalized, _) = Normalize(path);

        // The mock only knows about entries that have been explicitly registered.
        // If the path itself was never registered (and is not the parent of any
        // registered entry), mirror the real service's DirectoryNotFoundException.
        var pathRegistered = _entries.ContainsKey(normalized);
        var hasChildren = _entries.Keys.Any(k => ParentOf(k) == normalized);
        if (!pathRegistered && !hasChildren)
            throw new DirectoryNotFoundException($"Path not found: {normalized}");

        // Enumerate entries whose parent == normalized. Build a synthetic
        // FileSystemEntry for each, honouring the ignore list.
        var children = _entries
            .Where(kvp => ParentOf(kvp.Key) == normalized)
            .Where(kvp => !_ignoreList.IsIgnored(kvp.Key, kvp.Value.Kind == EntryKind.Directory))
            .Select(kvp => new FileSystemEntry(
                kvp.Value.Name,
                kvp.Key,
                kvp.Value.Kind == EntryKind.Directory
                    ? FileSystemEntryKind.Directory
                    : FileSystemEntryKind.File))
            .ToList();

        // Sort: directories first, then files, both alphabetical — mirrors the
        // real service's contract.
        var ordered = children
            .OrderBy(e => e.Kind == FileSystemEntryKind.Directory ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<FileSystemEntry>>(ordered);
    }

    public Task CreateFileAsync(string parentPath, string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (parent, _) = Normalize(parentPath);
        var target = Combine(parent, name);
        if (_entries.ContainsKey(target))
            throw new IOException($"File already exists: {target}");
        _entries[target] = new Entry(name, EntryKind.File);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (parent, _) = Normalize(parentPath);
        var target = Combine(parent, name);
        // Idempotent — mirrors Directory.CreateDirectory.
        _entries[target] = new Entry(name, EntryKind.Directory);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string path, string newName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (normalized, _) = Normalize(path);
        if (!_entries.TryGetValue(normalized, out var entry))
            throw new FileNotFoundException("Path not found.", normalized);
        var parent = ParentOf(normalized);
        var target = Combine(parent, newName);
        if (_entries.ContainsKey(target))
            throw new IOException($"Target already exists: {target}");
        _entries.Remove(normalized);
        _entries[target] = entry with { Name = newName };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (normalized, _) = Normalize(path);
        // Recursive: remove any entry whose path starts with `normalized + "/"`
        // OR equals it.
        var prefix = normalized + "/";
        var keys = _entries.Keys.Where(k => k == normalized || k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var k in keys) _entries.Remove(k);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (normalized, _) = Normalize(path);
        return Task.FromResult(_entries.ContainsKey(normalized));
    }

    // --- helpers -------------------------------------------------------

    private static (string Normalized, string Name) Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        var full = System.IO.Path.GetFullPath(path);
        var name = System.IO.Path.GetFileName(full);
        return (full, name);
    }

    private static string ParentOf(string normalized) =>
        System.IO.Path.GetDirectoryName(normalized)
        ?? System.IO.Path.GetPathRoot(normalized)
        ?? throw new InvalidOperationException("Path has no parent.");

    private static string Combine(string parent, string name) =>
        System.IO.Path.Combine(parent, name);
}
