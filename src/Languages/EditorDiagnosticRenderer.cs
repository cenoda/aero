using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Aero.Languages;

/// <summary>
/// Background renderer that draws diagnostic indicators for the active file.
/// Uses line-level background highlighting for errors/warnings.
/// 
/// Note: Falls back to simple highlighting if squiggle rendering proves unstable.
/// </summary>
public sealed class EditorDiagnosticRenderer : IBackgroundRenderer
{
    private readonly DiagnosticStore _diagnosticStore;
    private readonly Func<string?> _getActiveFileUri;

    public EditorDiagnosticRenderer(DiagnosticStore diagnosticStore, Func<string?> getActiveFileUri)
    {
        _diagnosticStore = diagnosticStore ?? throw new ArgumentNullException(nameof(diagnosticStore));
        _getActiveFileUri = getActiveFileUri ?? throw new ArgumentNullException(nameof(getActiveFileUri));
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var activeUri = _getActiveFileUri?.Invoke();
        if (string.IsNullOrEmpty(activeUri))
            return;

        var diagnostics = _diagnosticStore.GetDiagnostics(activeUri);
        if (diagnostics == null || diagnostics.Count == 0)
            return;

        var document = textView.Document;
        if (document == null)
            return;

        var lineCount = document.LineCount;
        var seenLines = new HashSet<int>();

        // Draw background highlights for lines with diagnostics.
        foreach (var diag in diagnostics)
        {
            // LSP uses 0-based line numbers; convert to 1-based for document.
            var docLineNumber = diag.Range.StartLine + 1;
            if (docLineNumber < 1 || docLineNumber > lineCount)
                continue;

            if (!seenLines.Add(diag.Range.StartLine))
                continue;

            // Get severity-based color (red for error, yellow for warning).
            var color = diag.Severity switch
            {
                DiagnosticSeverity.Error => Color.Parse("#33FF0000"),
                DiagnosticSeverity.Warning => Color.Parse("#33FFFF00"),
                DiagnosticSeverity.Information => Color.Parse("#3300FF00"),
                _ => (Color?)null
            };

            if (color == null)
                continue;

            try
            {
                var brush = new SolidColorBrush(color.Value);
                var line = document.GetLineByNumber(docLineNumber);
                var visualLine = textView.GetVisualLine(line.LineNumber);
                if (visualLine == null)
                    continue;

                var height = visualLine.Height;
                var y = visualLine.VisualTop - textView.ScrollOffset.Y;
                var width = textView.Bounds.Width;

                if (width <= 0 || width > 10000)
                    width = 2000; // Fallback width

                var rect = new Rect(0, y, width, height);
                drawingContext.DrawRectangle(brush, null, rect);
            }
            catch
            {
                // Silently ignore rendering errors - diagnostics are still
                // tracked and surfaced via the Problems panel (M4).
            }
        }
    }
}
