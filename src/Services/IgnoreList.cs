using System;
using System.Collections.Generic;

namespace Aero.Services;

/// <summary>
/// Default <see cref="IIgnoreList"/> implementation. Two pattern kinds:
/// <list type="bullet">
///   <item><b>Directory patterns</b> — bare names like <c>bin</c>, <c>node_modules</c>.
///         For a directory, its own leaf name must match. For a file, any
///         ancestor directory segment matching the pattern is enough — this is
///         how the M5 file system watcher filters out events from inside
///         ignored folders (e.g. <c>/repo/bin/Debug/app.dll</c>).</item>
///   <item><b>File patterns</b> — wildcard suffixes like <c>*.tmp</c>. Only
///         files are matched, by leaf name.</item>
/// </list>
/// Full glob parsing is intentionally out of scope — this is ~100 lines, no NuGet.
///
/// Case sensitivity follows the host OS:
/// <list type="bullet">
///   <item>Windows / macOS — case-insensitive.</item>
///   <item>Linux — case-sensitive.</item>
/// </list>
/// </summary>
public sealed class IgnoreList : IIgnoreList
{
    private readonly List<Pattern> _patterns = new();
    private readonly StringComparison _comparison;

    /// <summary>
    /// Sensible defaults for .NET / Node projects: build output, package caches,
    /// and VCS metadata. <c>*.tmp</c> hides editor temp files.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultPatterns = new[]
    {
        "node_modules",
        "bin",
        "obj",
        ".git",
        ".vs",
        "packages",
        "*.tmp",
    };

    public IgnoreList() : this(DefaultPatterns) { }

    public IgnoreList(IEnumerable<string> initialPatterns)
    {
        if (initialPatterns == null) throw new ArgumentNullException(nameof(initialPatterns));

        // OS-specific comparison — Windows file system is case-insensitive.
        _comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var pattern in initialPatterns)
            AddPattern(pattern);
    }

    /// <inheritdoc />
    public void AddPattern(string pattern)
    {
        if (pattern is null)
            throw new ArgumentNullException(nameof(pattern));
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern must not be empty.", nameof(pattern));

        // Classification: anything starting with "*." is a file-only wildcard;
        // everything else is a bare directory name.
        _patterns.Add(pattern.StartsWith("*.")
            ? new Pattern(PatternKind.File, pattern[1..])   // ".ext"
            : new Pattern(PatternKind.Directory, pattern));
    }

    /// <inheritdoc />
    public bool IsIgnored(string path, bool isDirectory)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Pre-segment once; GetDirectoryName is moderately expensive.
        var segments = GetSegments(path);
        if (segments.Length == 0)
            return false;

        var leaf = segments[^1];

        foreach (var pattern in _patterns)
        {
            if (pattern.Kind == PatternKind.Directory)
            {
                if (isDirectory)
                {
                    // A directory matches when its own leaf name matches.
                    if (string.Equals(leaf, pattern.Value, _comparison))
                        return true;
                }
                else
                {
                    // A file matches when ANY ancestor directory segment matches —
                    // the file is inside an ignored directory. This is the case
                    // M5's watcher relies on.
                    for (int i = 0; i < segments.Length - 1; i++)
                    {
                        if (string.Equals(segments[i], pattern.Value, _comparison))
                            return true;
                    }
                }
            }
            else // PatternKind.File — wildcard like "*.ext"
            {
                if (isDirectory)
                    continue; // file patterns never match directories
                if (leaf.EndsWith(pattern.Value, _comparison))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Split a path into its name segments. Empty segments (from leading or
    /// trailing separators) are dropped. A Windows drive prefix like <c>C:</c>
    /// is stripped so its empty remainder does not become a phantom segment.
    /// </summary>
    private static string[] GetSegments(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        if (normalized.Length == 0)
            return Array.Empty<string>();

        int start = 0;
        if (normalized.Length >= 2 && normalized[1] == ':')
            start = 2;
        if (start == normalized.Length)
            return Array.Empty<string>();

        var trimmed = normalized[start..];
        return trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private enum PatternKind { Directory, File }

    private readonly record struct Pattern(PatternKind Kind, string Value);
}
