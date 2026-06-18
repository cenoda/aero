using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Project;

namespace Aero.Services;

/// <summary>
/// Abstraction over <see cref="System.IO"/> for file explorer operations.
/// Implementations must honour <see cref="CancellationToken"/> on every
/// I/O-bound method so the UI never blocks on a large enumeration.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Enumerate the direct children of <paramref name="path"/>. Directories are
    /// listed before files, each group sorted alphabetically. Entries that match
    /// <see cref="IIgnoreList"/> are filtered out.
    /// </summary>
    Task<IReadOnlyList<FileSystemEntry>> GetDirectoryEntriesAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>Create an empty file at <c>parentPath/name</c>.</summary>
    Task CreateFileAsync(string parentPath, string name, CancellationToken ct = default);

    /// <summary>Create an empty directory at <c>parentPath/name</c>.</summary>
    Task CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default);

    /// <summary>
    /// Rename the file or directory at <paramref name="path"/> to <paramref name="newName"/>.
    /// The new name must be a leaf name (no directory separators).
    /// </summary>
    Task RenameAsync(string path, string newName, CancellationToken ct = default);

    /// <summary>Delete the file or directory at <paramref name="path"/>. Directories are removed recursively.</summary>
    Task DeleteAsync(string path, CancellationToken ct = default);

    /// <summary>Returns whether the path exists on disk.</summary>
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
}
