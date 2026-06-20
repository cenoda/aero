using System;

namespace Aero.Languages;

/// <summary>
/// A UI-friendly diagnostic record representing an LSP diagnostic message.
/// </summary>
public sealed class Diagnostic
{
    public Diagnostic(
        DiagnosticSeverity severity,
        string fileUri,
        TextRange range,
        string message,
        string? source = null,
        string? code = null)
    {
        Severity = severity;
        FileUri = fileUri ?? throw new ArgumentNullException(nameof(fileUri));
        Range = range ?? throw new ArgumentNullException(nameof(range));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Source = source;
        Code = code;
    }

    public DiagnosticSeverity Severity { get; }

    public string FileUri { get; }

    public TextRange Range { get; }

    public string Message { get; }

    public string? Source { get; }

    public string? Code { get; }
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
/// </summary>
public sealed class TextRange
{
    public TextRange(int startLine, int startCharacter, int endLine, int endCharacter)
    {
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
    }

    public int StartLine { get; }

    public int StartCharacter { get; }

    public int EndLine { get; }

    public int EndCharacter { get; }

    public override string ToString() =>
        $"({StartLine},{StartCharacter})-({EndLine},{EndCharacter})";
}
