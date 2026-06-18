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
