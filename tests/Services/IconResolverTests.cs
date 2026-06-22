using Aero.Services;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// Unit tests for <see cref="IconResolver"/>.
/// </summary>
public class IconResolverTests
{
    [Theory]
    [InlineData(".cs", "Icon.Code")]
    [InlineData(".csx", "Icon.Code")]
    [InlineData(".js", "Icon.Code")]
    [InlineData(".ts", "Icon.Code")]
    [InlineData(".tsx", "Icon.Code")]
    [InlineData(".jsx", "Icon.Code")]
    [InlineData(".py", "Icon.Code")]
    [InlineData(".rs", "Icon.Code")]
    [InlineData(".go", "Icon.Code")]
    [InlineData(".java", "Icon.Code")]
    [InlineData(".cpp", "Icon.Code")]
    [InlineData(".c", "Icon.Code")]
    [InlineData(".h", "Icon.Code")]
    [InlineData(".fs", "Icon.Code")]
    [InlineData(".sh", "Icon.Code")]
    [InlineData(".ps1", "Icon.Code")]
    [InlineData(".sql", "Icon.Code")]
    public void GetIconKey_ReturnsCode_ForCodeExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Theory]
    [InlineData(".txt", "Icon.Text")]
    [InlineData(".md", "Icon.Text")]
    [InlineData(".markdown", "Icon.Text")]
    [InlineData(".log", "Icon.Text")]
    [InlineData(".rst", "Icon.Text")]
    public void GetIconKey_ReturnsText_ForTextExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Theory]
    [InlineData(".png", "Icon.Image")]
    [InlineData(".jpg", "Icon.Image")]
    [InlineData(".jpeg", "Icon.Image")]
    [InlineData(".gif", "Icon.Image")]
    [InlineData(".svg", "Icon.Image")]
    [InlineData(".webp", "Icon.Image")]
    public void GetIconKey_ReturnsImage_ForImageExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Theory]
    [InlineData(".json", "Icon.Config")]
    [InlineData(".xml", "Icon.Config")]
    [InlineData(".yaml", "Icon.Config")]
    [InlineData(".yml", "Icon.Config")]
    [InlineData(".toml", "Icon.Config")]
    [InlineData(".ini", "Icon.Config")]
    [InlineData(".editorconfig", "Icon.Config")]
    [InlineData(".gitignore", "Icon.Config")]
    [InlineData(".props", "Icon.Config")]
    [InlineData(".targets", "Icon.Config")]
    public void GetIconKey_ReturnsConfig_ForConfigExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Theory]
    [InlineData(".html", "Icon.Markup")]
    [InlineData(".css", "Icon.Markup")]
    [InlineData(".scss", "Icon.Markup")]
    [InlineData(".axaml", "Icon.Markup")]
    [InlineData(".xaml", "Icon.Markup")]
    public void GetIconKey_ReturnsMarkup_ForMarkupExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Theory]
    [InlineData(".sln", "Icon.Project")]
    [InlineData(".csproj", "Icon.Project")]
    [InlineData(".fsproj", "Icon.Project")]
    public void GetIconKey_ReturnsProject_ForProjectExtensions(string ext, string expected)
    {
        Assert.Equal(expected, IconResolver.GetIconKey($"file{ext}"));
    }

    [Fact]
    public void GetIconKey_ReturnsUnknown_ForUnknownExtension()
    {
        Assert.Equal("Icon.Unknown", IconResolver.GetIconKey("file.xyz"));
    }

    [Fact]
    public void GetIconKey_ReturnsUnknown_ForNullPath()
    {
        Assert.Equal("Icon.Unknown", IconResolver.GetIconKey(null));
    }

    [Fact]
    public void GetIconKey_ReturnsUnknown_ForEmptyPath()
    {
        Assert.Equal("Icon.Unknown", IconResolver.GetIconKey(""));
    }

    [Fact]
    public void GetIconKey_ReturnsUnknown_ForNoExtension()
    {
        Assert.Equal("Icon.Unknown", IconResolver.GetIconKey("Makefile"));
    }

    [Fact]
    public void GetIconKey_IsCaseInsensitive()
    {
        Assert.Equal("Icon.Code", IconResolver.GetIconKey("File.CS"));
        Assert.Equal("Icon.Code", IconResolver.GetIconKey("File.PY"));
        Assert.Equal("Icon.Markup", IconResolver.GetIconKey("File.HTML"));
    }
}
