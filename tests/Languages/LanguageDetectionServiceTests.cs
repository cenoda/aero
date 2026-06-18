using Aero.Languages;
using Xunit;

namespace Aero.Tests.Languages;

public class LanguageDetectionServiceTests
{
    private readonly ILanguageDetectionService _service = new LanguageDetectionService();

    [Theory]
    [InlineData("foo.cs", "csharp", "C#")]
    [InlineData("Foo.CS", "csharp", "C#")]
    [InlineData("bar.json", "json", "JSON")]
    [InlineData("test.xml", "xml", "XML")]
    [InlineData("App.axaml", "xml", "XML")]
    [InlineData("App.xaml", "xml", "XAML")]
    [InlineData("aero.csproj", "xml", "XML")]
    [InlineData("readme.md", "markdown", "Markdown")]
    [InlineData("readme.markdown", "markdown", "Markdown")]
    [InlineData("script.js", "javascript", "JavaScript")]
    [InlineData("main.ts", "typescript", "TypeScript")]
    [InlineData("app.py", "python", "Python")]
    [InlineData("index.html", "html", "HTML")]
    [InlineData("index.htm", "html", "HTML")]
    [InlineData("style.css", "css", "CSS")]
    [InlineData("style.scss", "scss", "SCSS")]
    [InlineData("notes.txt", "plaintext", "Plain Text")]
    [InlineData("app.fs", "fsharp", "F#")]
    [InlineData("app.yaml", "yaml", "YAML")]
    [InlineData("app.yml", "yaml", "YAML")]
    [InlineData("query.sql", "sql", "SQL")]
    [InlineData("lib.rs", "rust", "Rust")]
    [InlineData("main.go", "go", "Go")]
    [InlineData("Main.java", "java", "Java")]
    [InlineData("app.cpp", "cpp", "C++")]
    [InlineData("app.cc", "cpp", "C++")]
    [InlineData("app.c", "c", "C")]
    [InlineData("app.h", "cpp", "C++")]
    [InlineData("deploy.sh", "shellscript", "Bash")]
    [InlineData("deploy.bash", "shellscript", "Bash")]
    [InlineData("script.ps1", "powershell", "PowerShell")]
    public void Detect_KnownExtension_ReturnsExpectedLanguage(string filePath, string expectedId, string expectedDisplayName)
    {
        var result = _service.Detect(filePath);

        Assert.Equal(expectedId, result.Id);
        Assert.Equal(expectedDisplayName, result.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Makefile")]
    [InlineData("Dockerfile")]
    [InlineData("unknown.unknown")]
    [InlineData("/path/to/file")]
    public void Detect_UnknownOrNullOrNoExtension_ReturnsPlainText(string? filePath)
    {
        var result = _service.Detect(filePath);

        Assert.Same(LanguageInfo.PlainText, result);
    }

    [Fact]
    public void Detect_CompoundName_Csproj_ReturnsXml()
    {
        var result = _service.Detect("Foo.csproj");

        Assert.Equal("xml", result.Id);
        Assert.Equal("XML", result.DisplayName);
    }

    [Fact]
    public void Detect_CaseInsensitive_UpperCaseExtension_ReturnsSameLanguage()
    {
        var lower = _service.Detect("file.cs");
        var upper = _service.Detect("file.CS");

        Assert.Equal(lower.Id, upper.Id);
        Assert.Equal(lower.DisplayName, upper.DisplayName);
    }
}
