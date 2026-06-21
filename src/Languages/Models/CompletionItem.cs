using System;

namespace Aero.Languages;

/// <summary>
/// LSP completion item returned from textDocument/completion.
/// </summary>
public sealed record CompletionItem(
    string Label,
    string? Kind = null,
    string? Detail = null,
    string? Documentation = null,
    int? InsertTextFormat = null)
{
    /// <summary>
    /// Factory method with null validation.
    /// </summary>
    public static CompletionItem Create(
        string label,
        string? kind = null,
        string? detail = null,
        string? documentation = null,
        int? insertTextFormat = null)
    {
        return new CompletionItem(
            label ?? throw new ArgumentNullException(nameof(label)),
            kind,
            detail,
            documentation,
            insertTextFormat);
    }
}