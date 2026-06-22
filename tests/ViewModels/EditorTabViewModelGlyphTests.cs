using Aero.Core;
using Aero.Models.Editor;
using Aero.ViewModels;
using NSubstitute;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorTabViewModel.Glyph"/> and
/// <see cref="FileExplorerNodeViewModel.Glyph"/> properties.
/// </summary>
public class EditorTabViewModelGlyphTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private EditorTabViewModel CreateTab(string? filePath)
    {
        var doc = filePath is null
            ? new TextDocument()
            : new TextDocument(string.Empty, filePath);
        return new EditorTabViewModel(doc, _bus, "plaintext");
    }

    [Theory]
    [InlineData("/home/user/project/file.cs", "Icon.Code")]
    [InlineData("/home/user/project/file.js", "Icon.Code")]
    [InlineData("/home/user/project/file.py", "Icon.Code")]
    [InlineData("/home/user/project/file.rs", "Icon.Code")]
    [InlineData("/home/user/project/file.go", "Icon.Code")]
    [InlineData("/home/user/project/file.txt", "Icon.Text")]
    [InlineData("/home/user/project/file.md", "Icon.Text")]
    [InlineData("/home/user/project/file.png", "Icon.Image")]
    [InlineData("/home/user/project/file.jpg", "Icon.Image")]
    [InlineData("/home/user/project/file.json", "Icon.Config")]
    [InlineData("/home/user/project/file.yaml", "Icon.Config")]
    [InlineData("/home/user/project/file.html", "Icon.Markup")]
    [InlineData("/home/user/project/file.css", "Icon.Markup")]
    [InlineData("/home/user/project/file.axaml", "Icon.Markup")]
    [InlineData("/home/user/project/file.sln", "Icon.Project")]
    [InlineData("/home/user/project/file.csproj", "Icon.Project")]
    [InlineData("/home/user/project/file.xyz", "Icon.Unknown")]
    public void Glyph_ReturnsCorrectKey_ForFilePath(string filePath, string expected)
    {
        using var tab = CreateTab(filePath);
        Assert.Equal(expected, tab.Glyph);
    }

    [Fact]
    public void Glyph_ReturnsUnknown_ForUntitled()
    {
        using var tab = CreateTab(null);
        Assert.Equal("Icon.Unknown", tab.Glyph);
    }

    /// <summary>
    /// Old IconKind values (pre-8.5) from FileExplorerNodeViewModel
    /// must still map to the correct icon keys.
    /// </summary>
    [Theory]
    [InlineData("MicrosoftVisualStudio", "Icon.Project")]
    [InlineData("LanguageCsharp", "Icon.Code")]
    [InlineData("Nodejs", "Icon.Config")]
    [InlineData("FileDocument", "Icon.Unknown")]
    [InlineData("Folder", "Icon.Folder")]
    [InlineData("Placeholder", "Icon.Unknown")]
    public void Glyph_OldKeys_MapCorrectly(string oldIconKind, string expected)
    {
        var node = new FileExplorerNodeViewModel("test", "/test/file", false, oldIconKind);
        Assert.Equal(expected, node.Glyph);
    }

    /// <summary>
    /// New IconResolver keys pass through directly.
    /// </summary>
    [Theory]
    [InlineData("Icon.Folder", "Icon.Folder")]
    [InlineData("Icon.Code", "Icon.Code")]
    [InlineData("Icon.Text", "Icon.Text")]
    [InlineData("Icon.Project", "Icon.Project")]
    public void Glyph_NewKeys_PassThrough(string newKey, string expected)
    {
        var node = new FileExplorerNodeViewModel("test", "/test/file", false, newKey);
        Assert.Equal(expected, node.Glyph);
    }
}
