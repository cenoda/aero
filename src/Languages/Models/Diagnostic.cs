using System;
using System.IO;

namespace Aero.Languages;

/// <summary>
/// A UI-friendly diagnostic record representing an LSP diagnostic message.
/// Provides value equality for deduplication in DiagnosticStore.
/// </summary>
public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string FileUri,
    TextRange Range,
    string Message,
    string? Source = null,
    string? Code = null)
{
    /// <summary>
    /// Factory method with null validation for API compatibility.
    /// </summary>
    public static Diagnostic Create(
        DiagnosticSeverity severity,
        string fileUri,
        TextRange range,
        string message,
        string? source = null,
        string? code = null)
    {
        return new Diagnostic(
            severity,
            fileUri ?? throw new ArgumentNullException(nameof(fileUri)),
            range ?? throw new ArgumentNullException(nameof(range)),
            message ?? throw new ArgumentNullException(nameof(message)),
            source,
            code);
    }

    /// <summary>
    /// Severity icon for display (e.g., "❌", "⚠️").
    /// </summary>
    public string SeverityIcon => Severity switch
    {
        DiagnosticSeverity.Error => "❌",
        DiagnosticSeverity.Warning => "⚠️",
        DiagnosticSeverity.Information => "ℹ️",
        DiagnosticSeverity.Hint => "💡",
        _ => "❓"
    };

    /// <summary>
    /// Severity color name for display (e.g., "Red", "Orange").
    /// </summary>
    public string SeverityColor => Severity switch
    {
        DiagnosticSeverity.Error => "Red",
        DiagnosticSeverity.Warning => "Orange",
        DiagnosticSeverity.Information => "Blue",
        DiagnosticSeverity.Hint => "Gray",
        _ => "Black"
    };

    /// <summary>
    /// File name extracted from the URI.
    /// </summary>
    public string FileName
    {
        get
        {
            if (string.IsNullOrEmpty(FileUri))
                return string.Empty;
            try
            {
                var uri = new Uri(FileUri);
                return Path.GetFileName(uri.LocalPath);
            }
            catch
            {
                return FileUri;
            }
        }
    }

    /// <summary>
    /// Location text for display (e.g., "Ln 5, Col 10").
    /// </summary>
    public string LocationText => $"Ln {Range.StartLine + 1}, Col {Range.StartCharacter + 1}";
}

/// <summary>
/// LSP diagnostic severity levels.
/// </summary>
public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}

/// <summary>
/// A range in a text document identified by start and end line/character positions.
/// Provides value equality for deduplication in DiagnosticStore.
/// </summary>
public sealed record TextRange(int StartLine, int StartCharacter, int EndLine, int EndCharacter)
{
    public override string ToString() =>
        $"({StartLine},{StartCharacter})-({EndLine},{EndCharacter})";
}
