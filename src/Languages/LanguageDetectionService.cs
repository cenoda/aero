using System;
using System.Collections.Generic;
using System.IO;

namespace Aero.Languages;

public class LanguageDetectionService : ILanguageDetectionService
{
    private static readonly Dictionary<string, LanguageInfo> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = new LanguageInfo("csharp", "C#"),
        [".csx"] = new LanguageInfo("csharp", "C#"),
        [".cake"] = new LanguageInfo("csharp", "C#"),

        [".fs"] = new LanguageInfo("fsharp", "F#"),
        [".fsi"] = new LanguageInfo("fsharp", "F#"),
        [".fsx"] = new LanguageInfo("fsharp", "F#"),

        [".json"] = new LanguageInfo("json", "JSON"),

        [".xml"] = new LanguageInfo("xml", "XML"),
        [".xsd"] = new LanguageInfo("xml", "XML"),
        [".axaml"] = new LanguageInfo("xml", "XML"),
        [".xaml"] = new LanguageInfo("xml", "XAML"),
        [".csproj"] = new LanguageInfo("xml", "XML"),
        [".fsproj"] = new LanguageInfo("xml", "XML"),
        [".props"] = new LanguageInfo("xml", "XML"),
        [".targets"] = new LanguageInfo("xml", "XML"),

        [".md"] = new LanguageInfo("markdown", "Markdown"),
        [".markdown"] = new LanguageInfo("markdown", "Markdown"),
        [".mdwn"] = new LanguageInfo("markdown", "Markdown"),
        [".mdown"] = new LanguageInfo("markdown", "Markdown"),
        [".mkd"] = new LanguageInfo("markdown", "Markdown"),
        [".mkdn"] = new LanguageInfo("markdown", "Markdown"),

        [".js"] = new LanguageInfo("javascript", "JavaScript"),
        [".es6"] = new LanguageInfo("javascript", "JavaScript"),
        [".mjs"] = new LanguageInfo("javascript", "JavaScript"),
        [".cjs"] = new LanguageInfo("javascript", "JavaScript"),

        [".ts"] = new LanguageInfo("typescript", "TypeScript"),
        [".tsx"] = new LanguageInfo("typescriptreact", "TypeScript JSX"),

        [".jsx"] = new LanguageInfo("javascriptreact", "JavaScript JSX"),

        [".py"] = new LanguageInfo("python", "Python"),
        [".pyw"] = new LanguageInfo("python", "Python"),

        [".html"] = new LanguageInfo("html", "HTML"),
        [".htm"] = new LanguageInfo("html", "HTML"),

        [".css"] = new LanguageInfo("css", "CSS"),
        [".scss"] = new LanguageInfo("scss", "SCSS"),

        [".txt"] = LanguageInfo.PlainText,

        [".yaml"] = new LanguageInfo("yaml", "YAML"),
        [".yml"] = new LanguageInfo("yaml", "YAML"),

        [".sql"] = new LanguageInfo("sql", "SQL"),

        [".rs"] = new LanguageInfo("rust", "Rust"),

        [".go"] = new LanguageInfo("go", "Go"),

        [".java"] = new LanguageInfo("java", "Java"),
        [".jav"] = new LanguageInfo("java", "Java"),

        [".cpp"] = new LanguageInfo("cpp", "C++"),
        [".cc"] = new LanguageInfo("cpp", "C++"),
        [".cxx"] = new LanguageInfo("cpp", "C++"),
        [".c++"] = new LanguageInfo("cpp", "C++"),
        [".hpp"] = new LanguageInfo("cpp", "C++"),
        [".hh"] = new LanguageInfo("cpp", "C++"),
        [".hxx"] = new LanguageInfo("cpp", "C++"),
        [".h"] = new LanguageInfo("cpp", "C++"),

        [".c"] = new LanguageInfo("c", "C"),
        [".i"] = new LanguageInfo("c", "C"),

        [".sh"] = new LanguageInfo("shellscript", "Bash"),
        [".bash"] = new LanguageInfo("shellscript", "Bash"),
        [".zsh"] = new LanguageInfo("shellscript", "Zsh"),

        [".ps1"] = new LanguageInfo("powershell", "PowerShell"),
        [".psm1"] = new LanguageInfo("powershell", "PowerShell"),
        [".psd1"] = new LanguageInfo("powershell", "PowerShell"),
    };

    public LanguageInfo Detect(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return LanguageInfo.PlainText;
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            return LanguageInfo.PlainText;
        }

        if (ExtensionMap.TryGetValue(extension, out var languageInfo))
        {
            return languageInfo;
        }

        return LanguageInfo.PlainText;
    }
}
