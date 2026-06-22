using System.IO;

namespace Aero.Services;

/// <summary>
/// Maps file extensions to icon resource keys used by <c>src/Styles/Icons.axaml</c>.
/// Pure static helper — no DI, no state.
/// </summary>
public static class IconResolver
{
    /// <summary>
    /// Returns the icon resource key (e.g. <c>"Icon.Code"</c>) for the given file path.
    /// Directories should be handled by the caller (return <c>"Icon.Folder"</c>).
    /// </summary>
    public static string GetIconKey(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "Icon.Unknown";
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
            return "Icon.Unknown";
        return ext.ToLowerInvariant() switch
        {
            // Code
            ".cs" or ".csx" or ".cake" => "Icon.Code",
            ".js" or ".mjs" or ".cjs" or ".es6" => "Icon.Code",
            ".ts" or ".tsx" or ".jsx" => "Icon.Code",
            ".py" or ".pyw" => "Icon.Code",
            ".rs" or ".go" => "Icon.Code",
            ".java" or ".jav" => "Icon.Code",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".hh" or ".hxx" => "Icon.Code",
            ".c" or ".h" or ".i" => "Icon.Code",
            ".fs" or ".fsi" or ".fsx" => "Icon.Code",
            ".sql" => "Icon.Code",
            ".sh" or ".bash" or ".zsh" => "Icon.Code",
            ".ps1" or ".psm1" or ".psd1" => "Icon.Code",

            // Text
            ".txt" => "Icon.Text",
            ".md" or ".markdown" or ".mdwn" or ".mdown" or ".mkd" or ".mkdn" => "Icon.Text",
            ".log" or ".rst" => "Icon.Text",

            // Image
            ".png" or ".jpg" or ".jpeg" or ".gif" => "Icon.Image",
            ".svg" or ".ico" or ".webp" or ".bmp" => "Icon.Image",

            // Config
            ".json" => "Icon.Config",
            ".xml" or ".xsd" => "Icon.Config",
            ".yaml" or ".yml" => "Icon.Config",
            ".toml" or ".ini" or ".cfg" => "Icon.Config",
            ".editorconfig" or ".gitignore" or ".gitattributes" => "Icon.Config",
            ".props" or ".targets" => "Icon.Config",

            // Markup
            ".html" or ".htm" => "Icon.Markup",
            ".css" or ".scss" or ".less" => "Icon.Markup",
            ".axaml" or ".xaml" => "Icon.Markup",

            // Project
            ".sln" or ".csproj" or ".fsproj" => "Icon.Project",

            _ => "Icon.Unknown",
        };
    }
}
