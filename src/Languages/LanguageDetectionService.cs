using System;
using System.Collections.Generic;
using System.IO;

namespace Aero.Languages;

public class LanguageDetectionService : ILanguageDetectionService
{
    private static readonly Dictionary<string, LanguageInfo> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = new LanguageInfo("csharp", "C#"),
        [".json"] = new LanguageInfo("json", "JSON"),
        [".xml"] = new LanguageInfo("xml", "XML"),
        [".axaml"] = new LanguageInfo("xml", "XML"),
        [".csproj"] = new LanguageInfo("xml", "XML"),
        [".md"] = new LanguageInfo("markdown", "Markdown"),
        [".markdown"] = new LanguageInfo("markdown", "Markdown"),
        [".js"] = new LanguageInfo("javascript", "JavaScript"),
        [".ts"] = new LanguageInfo("typescript", "TypeScript"),
        [".py"] = new LanguageInfo("python", "Python"),
        [".html"] = new LanguageInfo("html", "HTML"),
        [".css"] = new LanguageInfo("css", "CSS"),
        [".txt"] = new LanguageInfo("plaintext", "Plain Text"),
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
