using Aero.Core;
using Aero.Services;
using Aero.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Aero;

public partial class App : Application
{
    private IServiceProvider? _services;

/// <summary>Global DI service provider — available after OnFrameworkInitializationCompleted.</summary>
    internal static IServiceProvider Services =>
        ((App)Current!)._services
        ?? throw new InvalidOperationException("Services not yet initialized.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = _services.GetRequiredService<ShellViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = shell };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Core infrastructure
        services.AddSingleton<IMessageBus, MessageBus>();

        // Services
        services.AddSingleton<DocumentManager>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<FindReplaceViewModel>();

        return services.BuildServiceProvider();
    }
}
