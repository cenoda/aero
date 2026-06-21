using System.Collections.Generic;
using Aero.Core;
using Aero.Languages;
using Aero.Services;
using Aero.ViewModels;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Headless tests for the DiagnosticStore DI seam on EditorViewModel.
/// Verifies that:
///   1. The DiagnosticStore injected at construction is exposed via the property.
///   2. DiagnosticsChanged is raised when DiagnosticsUpdated arrives on the bus.
///   3. Disposing the VM stops DiagnosticsChanged from firing.
/// These tests require no Avalonia UI thread — DiagnosticStore and StubMessageBus
/// are both synchronous in-process objects.
/// </summary>
public class EditorViewModelDiagnosticsTests
{
    private static (EditorViewModel vm, StubMessageBus bus, IDocumentManagementService dm, DiagnosticStore store) Create()
    {
        var bus = new StubMessageBus();
        var languageDetection = new LanguageDetectionService();
        var dm = new DocumentManager(bus, languageDetection);
        var findReplace = new FindReplaceViewModel();
        var store = new DiagnosticStore(bus);
        var vm = new EditorViewModel(dm, bus, findReplace, languageDetection, store);
        return (vm, bus, dm, store);
    }

    [Fact]
    public void DiagnosticStore_Property_ReturnsSameInstanceAsInjected()
    {
        var (vm, _, _, store) = Create();

        Assert.Same(store, vm.DiagnosticStore);
    }

    [Fact]
    public void DiagnosticsChanged_RaisedWhen_DiagnosticsUpdated_Published()
    {
        var (vm, bus, _, store) = Create();

        var raisedCount = 0;
        vm.DiagnosticsChanged += () => raisedCount++;

        // DiagnosticStore.SetDiagnostics publishes DiagnosticsUpdated on the bus,
        // which the VM relays as DiagnosticsChanged.
        store.SetDiagnostics("lsp", "file:///test.cs", new List<Diagnostic>
        {
            new Diagnostic(
                DiagnosticSeverity.Error,
                "file:///test.cs",
                new TextRange(0, 0, 0, 5),
                "Test error",
                Code: "CS0001")
        });

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void DiagnosticsChanged_RaisedForEachUpdate()
    {
        var (vm, bus, _, store) = Create();

        var raisedCount = 0;
        vm.DiagnosticsChanged += () => raisedCount++;

        store.SetDiagnostics("lsp", "file:///a.cs", new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Warning, "file:///a.cs", new TextRange(0, 0, 0, 1), "warn")
        });
        store.SetDiagnostics("lsp", "file:///b.cs", new List<Diagnostic>
        {
            new Diagnostic(DiagnosticSeverity.Error, "file:///b.cs", new TextRange(1, 0, 1, 1), "err")
        });

        Assert.Equal(2, raisedCount);
    }

    [Fact]
    public void DiagnosticsChanged_NotRaisedAfterDispose()
    {
        var (vm, bus, _, store) = Create();

        var raisedCount = 0;
        vm.DiagnosticsChanged += () => raisedCount++;

        vm.Dispose();

        // Publishing after dispose must not invoke the handler (unsubscribed from bus)
        bus.Publish(new DiagnosticsUpdated(new List<Diagnostic>()));

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void DiagnosticsChanged_NotRaisedWhenDiagnosticsUnchanged()
    {
        var (vm, bus, _, store) = Create();

        var raisedCount = 0;
        vm.DiagnosticsChanged += () => raisedCount++;

        var diags = new List<Diagnostic>
        {
            new Diagnostic(
                DiagnosticSeverity.Error,
                "file:///test.cs",
                new TextRange(0, 0, 0, 5),
                "err",
                Code: "CS0001")
        };

        store.SetDiagnostics("lsp", "file:///test.cs", diags);
        Assert.Equal(1, raisedCount);

        // Setting the identical list again — DiagnosticStore short-circuits, no publish
        store.SetDiagnostics("lsp", "file:///test.cs", diags);
        Assert.Equal(1, raisedCount);
    }
}