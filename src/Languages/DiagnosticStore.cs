using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aero.Core;

namespace Aero.Languages;

/// <summary>
/// Stores the latest diagnostics per file URI and publishes DiagnosticsUpdated
/// messages when the set changes. One writer: LSPManager.
/// </summary>
public sealed class DiagnosticStore
{
    private readonly IMessageBus _bus;
    private readonly object _lock = new();
    private ImmutableDictionary<string, IReadOnlyList<Diagnostic>> _diagnosticsByFile = ImmutableDictionary<string, IReadOnlyList<Diagnostic>>.Empty;

    public DiagnosticStore(IMessageBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    /// <summary>
    /// Replace all diagnostics for a file URI. This replaces rather than accumulates
    /// per Plan §5.1 requirement.
    /// </summary>
    public void SetDiagnostics(string fileUri, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (fileUri == null)
            throw new ArgumentNullException(nameof(fileUri));

        var newList = diagnostics ?? Array.Empty<Diagnostic>();

        lock (_lock)
        {
            var existing = _diagnosticsByFile.GetValueOrDefault(fileUri);
            if (DiagnosticListsEqual(existing, newList))
                return;

            if (newList.Count == 0)
            {
                _diagnosticsByFile = _diagnosticsByFile.Remove(fileUri);
            }
            else
            {
                _diagnosticsByFile = _diagnosticsByFile.SetItem(fileUri, newList);
            }
        }

        PublishDiagnosticsUpdated();
    }

    /// <summary>
    /// Clear all diagnostics for a file URI (e.g., on document close).
    /// </summary>
    public void ClearDiagnostics(string fileUri)
    {
        if (fileUri == null)
            throw new ArgumentNullException(nameof(fileUri));

        lock (_lock)
        {
            if (!_diagnosticsByFile.ContainsKey(fileUri))
                return;

            _diagnosticsByFile = _diagnosticsByFile.Remove(fileUri);
        }

        PublishDiagnosticsUpdated();
    }

    /// <summary>
    /// Get all diagnostics for a specific file.
    /// </summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string fileUri)
    {
        if (fileUri == null)
            return Array.Empty<Diagnostic>();

        lock (_lock)
        {
            return _diagnosticsByFile.GetValueOrDefault(fileUri) ?? Array.Empty<Diagnostic>();
        }
    }

    /// <summary>
    /// Get all diagnostics in the workspace, flattened and ordered by file then by range.
    /// </summary>
    public IReadOnlyList<Diagnostic> GetAllDiagnostics()
    {
        lock (_lock)
        {
            return _diagnosticsByFile
                .OrderBy(kvp => kvp.Key)
                .SelectMany(kvp => kvp.Value)
                .ToList();
        }
    }

    private void PublishDiagnosticsUpdated()
    {
        IReadOnlyList<Diagnostic> all;
        lock (_lock)
        {
            all = _diagnosticsByFile
                .OrderBy(kvp => kvp.Key)
                .SelectMany(kvp => kvp.Value)
                .ToList();
        }

        _bus.Publish(new DiagnosticsUpdated(all));
    }

private static bool DiagnosticListsEqual(IReadOnlyList<Diagnostic>? left, IReadOnlyList<Diagnostic>? right)
    {
        if (left == right)
            return true;

        if (left == null || right == null || left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }

        return true;
    }
}
