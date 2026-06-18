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
    /// pattern. <paramref name="isDirectory"/> determines which pattern kinds
    /// can match:
    /// <list type="bullet">
    ///   <item><c>true</c> — bare-name directory patterns match by leaf name.</item>
    ///   <item><c>false</c> — bare-name patterns match when ANY ancestor
    ///         directory segment matches (the file is inside an ignored folder);
    ///         wildcard file patterns (<c>*.ext</c>) match by leaf name.</item>
    /// </list>
    /// </summary>
    bool IsIgnored(string path, bool isDirectory);

    /// <summary>Add a pattern to the ignore list. Patterns are case-insensitive on Windows.</summary>
    void AddPattern(string pattern);
}
