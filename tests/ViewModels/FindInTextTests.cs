using System;
using Aero.Core;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for TextSearchHelper.FindInText — the whole-word search logic
/// that was fixed in FIX_TODO.md item (dead variable + infinite-loop advance bug).
/// Extracted from EditorViewModel to a static helper for pure-function semantics.
/// </summary>
public class FindInTextTests
{
    // -----------------------------------------------------------------------
    // Non–whole-word (plain substring) searches
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_PlainSearch_FindsAtStart()
    {
        Assert.Equal(0, TextSearchHelper.FindInText("hello world", "hello", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_FindsInMiddle()
    {
        Assert.Equal(6, TextSearchHelper.FindInText("hello world", "world", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_ReturnsMinusOne_WhenNotFound()
    {
        Assert.Equal(-1, TextSearchHelper.FindInText("hello world", "xyz", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_CaseInsensitive_Finds()
    {
        Assert.Equal(0, TextSearchHelper.FindInText("Hello World", "hello", 0, StringComparison.OrdinalIgnoreCase, false));
    }

    [Fact]
    public void FindInText_PlainSearch_CaseSensitive_MissesWrongCase()
    {
        Assert.Equal(-1, TextSearchHelper.FindInText("Hello World", "hello", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_RespectsStartOffset()
    {
        // "test" appears at 0 and 11; searching from offset 1 should find the second
        Assert.Equal(11, TextSearchHelper.FindInText("test foobartest", "test", 1, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_OverlappingSubstring_FindsFirst()
    {
        // "foo" is a substring of "foobar" — should be found
        Assert.Equal(0, TextSearchHelper.FindInText("foobar", "foo", 0, StringComparison.Ordinal, false));
    }

    // -----------------------------------------------------------------------
    // Whole-word searches
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_WholeWord_FindsExactWord()
    {
        Assert.Equal(0, TextSearchHelper.FindInText("foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsSubstringMatch()
    {

        // "foo" embedded in "foobar" — must not match
        Assert.Equal(-1, TextSearchHelper.FindInText("foobar baz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsWhenPrecededByLetter()
    {

        // "bar" in "foobar" — preceded by 'r' which is a letter
        Assert.Equal(-1, TextSearchHelper.FindInText("foobar", "bar", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsWhenFollowedByLetter()
    {

        // "foo" in "foobar" — followed by 'b' which is a letter
        Assert.Equal(-1, TextSearchHelper.FindInText("foobar test", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FindsAtEnd()
    {

        // "bar" at end with no trailing character
        Assert.Equal(4, TextSearchHelper.FindInText("foo bar", "bar", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FindsAtStartOfString()
    {

        Assert.Equal(0, TextSearchHelper.FindInText("foo bar baz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_UnderscoreCountsAsWordChar_BeforeWord()
    {

        // "_foo" — preceded by underscore which is treated as word char, so not a whole word
        Assert.Equal(-1, TextSearchHelper.FindInText("_foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_UnderscoreCountsAsWordChar_AfterWord()
    {

        // "foo_" — followed by underscore, so not a whole word
        Assert.Equal(-1, TextSearchHelper.FindInText("foo_ bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_PunctuationBoundary_Matches()
    {

        // "foo" followed by comma is a word boundary
        Assert.Equal(0, TextSearchHelper.FindInText("foo, bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsFirstThenFindsSecond()
    {

        // "foo" embedded first ("foobar"), then standalone "foo" at index 11
        // This is the key regression test for the fixed advance-logic infinite-loop bug
        Assert.Equal(11, TextSearchHelper.FindInText("foobar baz foo qux", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_MultipleOccurrences_ReturnsFirstMatch()
    {

        Assert.Equal(0, TextSearchHelper.FindInText("foo bar foo", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_ReturnsMinusOne_WhenNoWholeWordMatch()
    {

        Assert.Equal(-1, TextSearchHelper.FindInText("foobar foobaz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_CaseInsensitive_Finds()
    {

        Assert.Equal(0, TextSearchHelper.FindInText("Foo bar", "foo", 0, StringComparison.OrdinalIgnoreCase, true));
    }

    [Fact]
    public void FindInText_WholeWord_CaseSensitive_MissesWrongCase()
    {

        Assert.Equal(-1, TextSearchHelper.FindInText("Foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_EmptyText_ReturnsMinusOne()
    {

        Assert.Equal(-1, TextSearchHelper.FindInText("", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SearchLongerThanText_ReturnsMinusOne()
    {

        Assert.Equal(-1, TextSearchHelper.FindInText("hi", "hello", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_StartOffsetPastFirstOccurrence_FindsSecond()
    {

        // "foo" at index 0 and 8; start from 1 → must find the one at 8
        Assert.Equal(8, TextSearchHelper.FindInText("foo bar foo", "foo", 1, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SingleCharWord_Matches()
    {

        // standalone "a"
        Assert.Equal(4, TextSearchHelper.FindInText("foo a bar", "a", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SingleCharWord_SkipsEmbedded()
    {

        // "a" embedded in "bar" — must not match
        Assert.Equal(-1, TextSearchHelper.FindInText("foobar", "a", 0, StringComparison.Ordinal, true));
    }

    // -----------------------------------------------------------------------
    // Digit boundary checks (digits are treated as word chars)
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_WholeWord_PrecededByDigit_NotAWordBoundary()
    {

        // "1foo" — preceded by digit, should NOT match as whole word
        Assert.Equal(-1, TextSearchHelper.FindInText("1foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FollowedByDigit_NotAWordBoundary()
    {

        // "foo2" — followed by digit, should NOT match as whole word
        Assert.Equal(-1, TextSearchHelper.FindInText("foo2 bar", "foo", 0, StringComparison.Ordinal, true));
    }
}
