namespace Aero.Services;

/// <summary>
/// Decides whether a path should be hidden from the file explorer. Used by
/// <c>IFileSystemService</c> when enumerating directories and by the future
/// file system watcher to filter its events.
/// </summary>
public interface IIgnoreList
{
    /// <summary>
    /// Returns <c>true</c> when the given path matches a configured ignore
    /// pattern. The <paramref name="isDirectory"/> flag lets the implementation
    /// apply folder-only or file-only patterns as needed.
    /// </summary>
    bool IsIgnored(string path, bool isDirectory);

    /// <summary>Add a pattern to the ignore list. Patterns are case-insensitive on Windows.</summary>
    void AddPattern(string pattern);
}
