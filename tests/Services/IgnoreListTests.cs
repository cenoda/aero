using Aero.Services;
using Xunit;

namespace Aero.Tests.Services;

public class IgnoreListTests
{
    // -------------------------------------------------------------------
    // Default patterns
    // -------------------------------------------------------------------

    [Fact]
    public void Defaults_IgnoreBinDirectory()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/bin", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnoreObjDirectory()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/src/obj", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnoreNodeModulesDirectory()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/node_modules", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnoreDotGit()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/.git", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnoreDotVs()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/.vs", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnorePackages()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/packages", isDirectory: true));
    }

    [Fact]
    public void Defaults_IgnoreTmpSuffix_Files()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/foo.tmp", isDirectory: false));
    }

    [Fact]
    public void Defaults_DoNotIgnoreRegularFile()
    {
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/README.md", isDirectory: false));
    }

    [Fact]
    public void Defaults_DoNotIgnoreRegularDirectory()
    {
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/src", isDirectory: true));
    }

    // -------------------------------------------------------------------
    // Pattern matching semantics
    // -------------------------------------------------------------------

    [Fact]
    public void MatchIsByLeafName_NotByPathPrefix()
    {
        // "bin" deep inside should still be ignored (folder name match).
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/very/deep/path/bin", isDirectory: true));
    }

    [Fact]
    public void MatchDoesNotIgnorePartialName()
    {
        // "bin" pattern must not match "binary" — exact leaf match, not prefix.
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/binary", isDirectory: true));
    }

    // -------------------------------------------------------------------
    // Segment-aware matching (regression for M5 watcher filtering)
    // -------------------------------------------------------------------

    [Fact]
    public void File_InsideIgnoredDirectory_IsIgnored()
    {
        // This is the M5 watcher case: an event for a file inside /repo/bin/
        // must be filtered out so build-output churn doesn't refresh the tree.
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/bin/Debug/app.dll", isDirectory: false));
    }

    [Fact]
    public void File_DeepInsideIgnoredDirectory_IsIgnored()
    {
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/node_modules/lodash/index.js", isDirectory: false));
    }

    [Fact]
    public void File_NamedLikeIgnoredDirectory_ButNotInside_IsNotIgnored()
    {
        // A file literally named "bin" at /repo/bin is NOT in an ignored
        // ancestor (its ancestors are [/repo]), so it should pass through.
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/bin", isDirectory: false));
    }

    [Fact]
    public void File_InsideNonIgnoredDirectory_IsNotIgnored()
    {
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/src/app.cs", isDirectory: false));
    }

    [Fact]
    public void File_BackslashSeparators_AreTreatedAsPathSeparators()
    {
        // Windows paths use "\\" — the matcher must split on both separators.
        var list = new IgnoreList();
        Assert.True(list.IsIgnored(@"C:\repo\bin\app.dll", isDirectory: false));
    }

    [Fact]
    public void Directory_WithTrailingSeparator_StillMatches()
    {
        // Path normalization on input is the caller's job, but a trailing
        // separator must not create a phantom empty segment.
        var list = new IgnoreList();
        Assert.True(list.IsIgnored("/repo/bin/", isDirectory: true));
    }

    // -------------------------------------------------------------------
    // isDirectory flag actually matters
    // -------------------------------------------------------------------

    [Fact]
    public void WildcardPattern_DoesNotMatchDirectory()
    {
        // *.tmp must only match files; a directory named "x.tmp" stays visible.
        var list = new IgnoreList();
        list.AddPattern("*.tmp");
        Assert.True(list.IsIgnored("/repo/x.tmp", isDirectory: false));
        Assert.False(list.IsIgnored("/repo/x.tmp", isDirectory: true));
    }

    [Fact]
    public void DirectoryPattern_FileWithSameLeafName_IsNotIgnoredByLeafAlone()
    {
        // A file named "bin" must not be auto-hidden — the directory pattern
        // "bin" only fires when an ancestor directory matches.
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("/repo/bin", isDirectory: false));
    }

    [Fact]
    public void WildcardSuffixMatchesAnyFileWithExtension()
    {
        var list = new IgnoreList();
        list.AddPattern("*.log");
        Assert.True(list.IsIgnored("/repo/server.log", isDirectory: false));
        Assert.False(list.IsIgnored("/repo/server.txt", isDirectory: false));
    }

    [Fact]
    public void EmptyPath_ReturnsFalse()
    {
        var list = new IgnoreList();
        Assert.False(list.IsIgnored("", isDirectory: true));
    }

    // -------------------------------------------------------------------
    // AddPattern validation
    // -------------------------------------------------------------------

    [Fact]
    public void AddPattern_Null_Throws()
    {
        var list = new IgnoreList();
        Assert.Throws<System.ArgumentNullException>(() => list.AddPattern(null!));
    }

    [Fact]
    public void AddPattern_Empty_Throws()
    {
        var list = new IgnoreList();
        Assert.Throws<System.ArgumentException>(() => list.AddPattern(""));
    }

    [Fact]
    public void AddPattern_Whitespace_Throws()
    {
        var list = new IgnoreList();
        Assert.Throws<System.ArgumentException>(() => list.AddPattern("   "));
    }

    [Fact]
    public void AddPattern_ThenIsIgnored_Applies()
    {
        var list = new IgnoreList();
        list.AddPattern("dist");
        Assert.True(list.IsIgnored("/repo/dist", isDirectory: true));
    }

    // -------------------------------------------------------------------
    // Custom initial patterns
    // -------------------------------------------------------------------

    [Fact]
    public void Constructor_CustomPatterns_ReplacesDefaults()
    {
        // The custom list excludes the defaults — only "build" is ignored.
        var list = new IgnoreList(new[] { "build" });
        Assert.True(list.IsIgnored("/repo/build", isDirectory: true));
        Assert.False(list.IsIgnored("/repo/bin", isDirectory: true));
        Assert.False(list.IsIgnored("/repo/node_modules", isDirectory: true));
    }
}
