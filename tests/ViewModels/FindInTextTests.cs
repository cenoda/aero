using System;
using Aero.Tests.Stubs;
using Aero.Services;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for EditorViewModel.FindInText — the whole-word search logic
/// that was fixed in FIX_TODO.md item (dead variable + infinite-loop advance bug).
/// </summary>
public class FindInTextTests
{
    // -----------------------------------------------------------------------
    // Helper — create a minimal EditorViewModel (no Avalonia UI bootstrap needed)
    // -----------------------------------------------------------------------

    private static EditorViewModel CreateVm()
    {
        var bus = new StubMessageBus();
        var dm = new DocumentManager(bus);
        var findReplace = new FindReplaceViewModel();
        return new EditorViewModel(dm, bus, findReplace);
    }

    // -----------------------------------------------------------------------
    // Non–whole-word (plain substring) searches
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_PlainSearch_FindsAtStart()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("hello world", "hello", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_FindsInMiddle()
    {
        var vm = CreateVm();
        Assert.Equal(6, vm.FindInText("hello world", "world", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_ReturnsMinusOne_WhenNotFound()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("hello world", "xyz", 0, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_CaseInsensitive_Finds()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("Hello World", "hello", 0, StringComparison.OrdinalIgnoreCase, false));
    }

    [Fact]
    public void FindInText_PlainSearch_CaseSensitive_MissesWrongCase()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("Hello World", "hello", 0, StringComparison.Ordinal, false));
    }

[Fact]
    public void FindInText_PlainSearch_RespectsStartOffset()
    {
        var vm = CreateVm();
        // "test" appears at 0 and 11; searching from offset 1 should find the second
        Assert.Equal(11, vm.FindInText("test foobartest", "test", 1, StringComparison.Ordinal, false));
    }

    [Fact]
    public void FindInText_PlainSearch_OverlappingSubstring_FindsFirst()
    {
        var vm = CreateVm();
        // "foo" is a substring of "foobar" — should be found
        Assert.Equal(0, vm.FindInText("foobar", "foo", 0, StringComparison.Ordinal, false));
    }

    // -----------------------------------------------------------------------
    // Whole-word searches
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_WholeWord_FindsExactWord()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsSubstringMatch()
    {
        var vm = CreateVm();
        // "foo" embedded in "foobar" — must not match
        Assert.Equal(-1, vm.FindInText("foobar baz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsWhenPrecededByLetter()
    {
        var vm = CreateVm();
        // "bar" in "foobar" — preceded by 'r' which is a letter
        Assert.Equal(-1, vm.FindInText("foobar", "bar", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SkipsWhenFollowedByLetter()
    {
        var vm = CreateVm();
        // "foo" in "foobar" — followed by 'b' which is a letter
        Assert.Equal(-1, vm.FindInText("foobar test", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FindsAtEnd()
    {
        var vm = CreateVm();
        // "bar" at end with no trailing character
        Assert.Equal(4, vm.FindInText("foo bar", "bar", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FindsAtStartOfString()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("foo bar baz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_UnderscoreCountsAsWordChar_BeforeWord()
    {
        var vm = CreateVm();
        // "_foo" — preceded by underscore which is treated as word char, so not a whole word
        Assert.Equal(-1, vm.FindInText("_foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_UnderscoreCountsAsWordChar_AfterWord()
    {
        var vm = CreateVm();
        // "foo_" — followed by underscore, so not a whole word
        Assert.Equal(-1, vm.FindInText("foo_ bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_PunctuationBoundary_Matches()
    {
        var vm = CreateVm();
        // "foo" followed by comma is a word boundary
        Assert.Equal(0, vm.FindInText("foo, bar", "foo", 0, StringComparison.Ordinal, true));
    }

[Fact]
    public void FindInText_WholeWord_SkipsFirstThenFindsSecond()
    {
        var vm = CreateVm();
        // "foo" embedded first ("foobar"), then standalone "foo" at index 11
        // This is the key regression test for the fixed advance-logic infinite-loop bug
        Assert.Equal(11, vm.FindInText("foobar baz foo qux", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_MultipleOccurrences_ReturnsFirstMatch()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("foo bar foo", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_ReturnsMinusOne_WhenNoWholeWordMatch()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("foobar foobaz", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_CaseInsensitive_Finds()
    {
        var vm = CreateVm();
        Assert.Equal(0, vm.FindInText("Foo bar", "foo", 0, StringComparison.OrdinalIgnoreCase, true));
    }

    [Fact]
    public void FindInText_WholeWord_CaseSensitive_MissesWrongCase()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("Foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_EmptyText_ReturnsMinusOne()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SearchLongerThanText_ReturnsMinusOne()
    {
        var vm = CreateVm();
        Assert.Equal(-1, vm.FindInText("hi", "hello", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_StartOffsetPastFirstOccurrence_FindsSecond()
    {
        var vm = CreateVm();
        // "foo" at index 0 and 8; start from 1 → must find the one at 8
        Assert.Equal(8, vm.FindInText("foo bar foo", "foo", 1, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SingleCharWord_Matches()
    {
        var vm = CreateVm();
        // standalone "a"
        Assert.Equal(4, vm.FindInText("foo a bar", "a", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_SingleCharWord_SkipsEmbedded()
    {
        var vm = CreateVm();
        // "a" embedded in "bar" — must not match
        Assert.Equal(-1, vm.FindInText("foobar", "a", 0, StringComparison.Ordinal, true));
    }

    // -----------------------------------------------------------------------
    // Digit boundary checks (digits are treated as word chars)
    // -----------------------------------------------------------------------

    [Fact]
    public void FindInText_WholeWord_PrecededByDigit_NotAWordBoundary()
    {
        var vm = CreateVm();
        // "1foo" — preceded by digit, should NOT match as whole word
        Assert.Equal(-1, vm.FindInText("1foo bar", "foo", 0, StringComparison.Ordinal, true));
    }

    [Fact]
    public void FindInText_WholeWord_FollowedByDigit_NotAWordBoundary()
    {
        var vm = CreateVm();
        // "foo2" — followed by digit, should NOT match as whole word
        Assert.Equal(-1, vm.FindInText("foo2 bar", "foo", 0, StringComparison.Ordinal, true));
    }
}
