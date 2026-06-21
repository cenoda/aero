using System;
using System.Collections.Generic;
using System.Linq;
using Aero.Core;
using Aero.Languages;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.Languages;

public class DiagnosticStoreTests
{
    [Fact]
    public void SetDiagnostics_ReplacesNotAccumulates_PerUri()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri = "file:///test.cs";
        var diagnostics1 = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "error 1")
        };

        var diagnostics2 = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(1, 0, 1, 5), "error 2")
        };

        // Set first diagnostics
        store.SetDiagnostics("lsp", uri, diagnostics1);
        Assert.Single(store.GetDiagnostics(uri));

        // Replace with second diagnostics - should NOT accumulate
        store.SetDiagnostics("lsp", uri, diagnostics2);
        Assert.Single(store.GetDiagnostics(uri));
        Assert.Equal("error 2", store.GetDiagnostics(uri)[0].Message);
    }

    [Fact]
    public void GetAllDiagnostics_FlattenedAndOrdered_ByFileThenRange()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri1 = "file:///a.cs";
        var uri2 = "file:///b.cs";

        store.SetDiagnostics("lsp", uri1, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri1, new TextRange(0, 0, 0, 5), "a-error")
        });

        store.SetDiagnostics("lsp", uri2, new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Warning, uri2, new TextRange(1, 0, 1, 5), "b-warning")
        });

        var all = store.GetAllDiagnostics();
        Assert.Equal(2, all.Count);

        // Should be ordered by file (a.cs before b.cs)
        Assert.Equal("a-error", all[0].Message);
        Assert.Equal("b-warning", all[1].Message);
    }

    [Fact]
    public void DiagnosticsUpdated_Raised_OnSet()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri = "file:///test.cs";
        var diagnostics = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "error")
        };

        store.SetDiagnostics("lsp", uri, diagnostics);

        // At least one DiagnosticsUpdated should have been raised
        var updated = bus.MessagesOf<DiagnosticsUpdated>().ToList();
        Assert.NotEmpty(updated);
        Assert.Single(updated[0].Diagnostics);
    }

    [Fact]
    public void DiagnosticsUpdated_Raised_OnClear()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri = "file:///test.cs";
        var diagnostics = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "error")
        };

        // First set, then clear - both should publish DiagnosticsUpdated messages
        store.SetDiagnostics("lsp", uri, diagnostics);
        store.ClearDiagnostics("lsp", uri);

        // At least one update with empty diagnostics was raised
        var updated = bus.MessagesOf<DiagnosticsUpdated>().ToList();
        Assert.NotEmpty(updated);
        
        // The last update should have empty diagnostics (after clear)
        var lastUpdate = updated.Last();
        Assert.Empty(lastUpdate.Diagnostics);
    }

    [Fact]
    public void ClearDiagnostics_OnClose_ClearsFileDiagnostics()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri = "file:///test.cs";
        var diagnostics = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "error")
        };

        store.SetDiagnostics("lsp", uri, diagnostics);
        Assert.NotEmpty(store.GetDiagnostics(uri));

        store.ClearDiagnostics("lsp", uri);
        Assert.Empty(store.GetDiagnostics(uri));
    }

    [Fact]
    public void SetDiagnostics_EmptyList_RemovesFile()
    {
        var bus = new StubMessageBus();
        var store = new DiagnosticStore(bus);

        var uri = "file:///test.cs";
        var diagnostics = new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, uri, new TextRange(0, 0, 0, 5), "error")
        };

        store.SetDiagnostics("lsp", uri, diagnostics);
        Assert.NotEmpty(store.GetDiagnostics(uri));

        // Set empty list should remove the file
        store.SetDiagnostics("lsp", uri, new List<Diagnostic>());
        var result = store.GetDiagnostics(uri);
        Assert.Empty(result);
    }
}
