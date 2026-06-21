using System;
using Aero.Core;
using Aero.Languages;
using Aero.Services;
using Aero.Tests.Stubs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// Regression tests for DI registration to ensure the split-singleton bug doesn't
/// regress. The bug was: both IDocumentManagementService and DocumentManager were
/// registered as separate singletons, causing LSPManager to have a different
/// instance than the editors, breaking LSP diagnostics.
///
/// NOTE: This test uses inline registrations rather than calling App.BuildServices()
/// to avoid the complexity of including App.axaml.cs (which pulls in Avalonia UI types).
/// In the future, if we extract the service registration into a separate
/// static method (e.g., ServiceCollectionExtensions.AddAeroServices()) that both
/// App.axaml.cs and tests can call, this test should be updated to use that method
/// for true production-wiring coverage.
/// </summary>
public class DocumentManagerDiTests
{
    [Fact]
    public void DocumentManager_And_IDocumentManagementService_ResolveToSameInstance()
    {
        // Build a ServiceProvider using the correct registration pattern.
        // This pattern MUST match what's in App.axaml.cs:BuildServices():
        //   1. Register the concrete type first
        //   2. Register the interface to resolve to the same instance
        var services = new ServiceCollection();

        // Register dependencies that DocumentManager requires
        services.AddSingleton<IMessageBus, StubMessageBus>();
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();

        // The correct pattern: concrete first, then interface using factory lambda
        services.AddSingleton<DocumentManager>();
        services.AddSingleton<IDocumentManagementService>(sp => sp.GetRequiredService<DocumentManager>());

        var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<DocumentManager>();
        var interfaceInstance = provider.GetRequiredService<IDocumentManagementService>();

        // Both should be the exact same instance (reference equality)
        Assert.Same(concrete, interfaceInstance);
    }
}
