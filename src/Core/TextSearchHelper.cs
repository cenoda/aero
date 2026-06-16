using System;

namespace Aero.Core;

/// <summary>
/// Static text search utilities used by the editor's find/replace functionality.
/// Extracted from EditorViewModel to keep it a pure function with no ViewModel state.
/// </summary>
public static class TextSearchHelper
{
    /// <summary>
    /// Find the next occurrence of <paramref name="search"/> in <paramref name="text"/>
    /// starting at <paramref name="start"/>. Returns the index or -1 if not found.
    /// </summary>
    public static int FindInText(string text, string search, int start,
        StringComparison comparison, bool wholeWord)
    {
        if (!wholeWord)
            return text.IndexOf(search, start, comparison);

        var idx = start;
        while ((idx = text.IndexOf(search, idx, comparison)) >= 0)
        {
            var before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]) && text[idx - 1] != '_';
            var end = idx + search.Length;
            var after = end >= text.Length || !char.IsLetterOrDigit(text[end]) && text[end] != '_';
            if (before && after) return idx;
            idx += search.Length;
        }
        return -1;
    }
}
