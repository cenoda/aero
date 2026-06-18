using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aero.Services;

/// <summary>
/// Default <see cref="IIgnoreList"/> implementation. Matches by exact folder/file
/// name (the last path segment), or by <c>*</c> wildcard suffix. No full glob
/// support — this is intentionally small and dependency-free.
///
/// Case sensitivity follows the host OS:
///   • Windows / macOS (default file systems) — case-insensitive.
///   • Linux — case-sensitive.
///
/// Eager tree loading combined with a folder like <c>node_modules</c> can list
/// tens of thousands of entries and freeze the UI thread. This list keeps those
/// directories out of the tree by default.
/// </summary>
public sealed class IgnoreList : IIgnoreList
{
    private readonly List<string> _patterns = new();
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
        _patterns.Add(pattern);
    }

    /// <inheritdoc />
    public bool IsIgnored(string path, bool isDirectory)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Compare only against the leaf name — we match by name, not by path prefix.
        // Using the OS-correct comparison so "Bin" matches "bin" on Windows but not Linux.
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (var pattern in _patterns)
        {
            if (Matches(name, pattern))
                return true;
        }
        return false;
    }

    private bool Matches(string name, string pattern)
    {
        // Simple wildcard: only "*.ext" suffix is supported. Anything else is an
        // exact name match. This is intentional — full glob parsing belongs in a
        // dedicated library, not here.
        if (pattern.StartsWith("*."))
        {
            var suffix = pattern[1..]; // ".ext"
            return name.EndsWith(suffix, _comparison);
        }
        return string.Equals(name, pattern, _comparison);
    }
}
