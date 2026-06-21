using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aero.Core;

namespace Aero.Languages;

/// <summary>
/// Event args for DiagnosticsUpdated events from DiagnosticStore.
/// </summary>
public sealed class DiagnosticsUpdatedEventArgs : EventArgs
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public DiagnosticsUpdatedEventArgs(IReadOnlyList<Diagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }
}

/// <summary>
/// Stores the latest diagnostics per (source, file URI) and publishes DiagnosticsUpdated
/// messages when the set changes. Supports multiple sources (e.g., LSP, build).
/// </summary>
public sealed class DiagnosticStore
{
    private readonly IMessageBus _bus;
    private readonly object _lock = new();
    private ImmutableDictionary<(string Source, string Uri), IReadOnlyList<Diagnostic>> _diagnosticsByFile =
        ImmutableDictionary<(string, string), IReadOnlyList<Diagnostic>>.Empty;

    /// <summary>
    /// Event published when diagnostics change (for direct subscription without message bus).
    /// </summary>
    public event EventHandler<DiagnosticsUpdatedEventArgs>? DiagnosticsUpdated;

    public DiagnosticStore(IMessageBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    /// <summary>
    /// Replace all diagnostics for a source+file URI. This replaces rather than accumulates.
    /// </summary>
    public void SetDiagnostics(string source, string fileUri, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (fileUri == null)
            throw new ArgumentNullException(nameof(fileUri));

        var newList = diagnostics ?? Array.Empty<Diagnostic>();
        var key = (source, fileUri);

        lock (_lock)
        {
            var existing = _diagnosticsByFile.GetValueOrDefault(key);
            if (DiagnosticListsEqual(existing, newList))
                return;

            if (newList.Count == 0)
            {
                _diagnosticsByFile = _diagnosticsByFile.Remove(key);
            }
            else
            {
                _diagnosticsByFile = _diagnosticsByFile.SetItem(key, newList);
            }
        }

        PublishDiagnosticsUpdated();
    }

    /// <summary>
    /// Clear all diagnostics for a source+file URI.
    /// </summary>
    public void ClearDiagnostics(string source, string fileUri)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (fileUri == null)
            throw new ArgumentNullException(nameof(fileUri));

        var key = (source, fileUri);

        lock (_lock)
        {
            if (!_diagnosticsByFile.ContainsKey(key))
                return;

            _diagnosticsByFile = _diagnosticsByFile.Remove(key);
        }

        PublishDiagnosticsUpdated();
    }

    /// <summary>
    /// Clear all diagnostics for a source (e.g., "build" before a new build).
    /// </summary>
    public void ClearSource(string source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        lock (_lock)
        {
            var keysToRemove = _diagnosticsByFile.Keys
                .Where(k => k.Source == source)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _diagnosticsByFile = _diagnosticsByFile.Remove(key);
            }
        }

        PublishDiagnosticsUpdated();
    }

    /// <summary>
    /// Get all diagnostics for a specific file (merges across sources).
    /// </summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string fileUri)
    {
        if (fileUri == null)
            return Array.Empty<Diagnostic>();

        lock (_lock)
        {
            return _diagnosticsByFile
                .Where(kvp => kvp.Key.Uri == fileUri)
                .SelectMany(kvp => kvp.Value)
                .ToList();
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
                .OrderBy(kvp => kvp.Key.Uri)
                .ThenBy(kvp => kvp.Key.Source)
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
        DiagnosticsUpdated?.Invoke(this, new DiagnosticsUpdatedEventArgs(all));
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
