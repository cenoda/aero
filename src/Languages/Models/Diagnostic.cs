using System;

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
