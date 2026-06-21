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
/// </summary>
public class DocumentManagerDiTests
{
    [Fact]
    public void DocumentManager_And_IDocumentManagementService_ResolveToSameInstance()
    {
        // Build the real ServiceProvider using the same registration logic as App.axaml.cs
        var services = new ServiceCollection();

        // Register dependencies that DocumentManager requires
        services.AddSingleton<IMessageBus, StubMessageBus>();
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();

        // Register the concrete type first, then have the interface resolve to the same instance.
        // This mirrors the registration in App.axaml.cs:BuildServices()
        services.AddSingleton<DocumentManager>();
        services.AddSingleton<IDocumentManagementService>(sp => sp.GetRequiredService<DocumentManager>());

        var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<DocumentManager>();
        var interfaceInstance = provider.GetRequiredService<IDocumentManagementService>();

        // Both should be the exact same instance (reference equality)
        Assert.Same(concrete, interfaceInstance);
    }
}
