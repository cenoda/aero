using Aero.Core;
using Aero.Languages;
using Aero.Services;
using Aero.Terminal;
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
            var bus = _services.GetRequiredService<IMessageBus>();

            // Eagerly resolve LSPManager so it subscribes to FolderOpened before any
            // folder is opened and is disposed with the DI container on app exit.
            _services.GetRequiredService<LSPManager>();

            var mainWindow = new MainWindow { DataContext = shell };
            mainWindow.Initialize(bus);
            desktop.MainWindow = mainWindow;

            // Optional startup folder: `aero /path/to/folder` opens that folder
            // immediately (useful for manual/automated smoke tests). This is
            // additive to the File → Open Folder picker flow implemented in M3.
            if (desktop.Args is { Length: > 0 } args && System.IO.Directory.Exists(args[0]))
            {
                bus.Publish(new FolderOpened(System.IO.Path.GetFullPath(args[0])));
            }

            // Dispose the DI container on application exit so all IDisposable
            // singletons (ShellViewModel, EditorViewModel, ...) get torn down and
            // their MessageBus subscriptions released. Covers both exit paths:
            // the Exit menu command and the window close ("X") button.
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        (_services as IDisposable)?.Dispose();
    }

private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Core infrastructure
        services.AddSingleton<IMessageBus, MessageBus>();

        // Services
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();
        // Register the concrete type first, then have the interface resolve to the same instance.
        services.AddSingleton<DocumentManager>();
        services.AddSingleton<IDocumentManagementService>(sp => sp.GetRequiredService<DocumentManager>());

        // Phase 4 — LSP integration
        services.AddSingleton<Func<string, string?, LSPSession>>(provider =>
        {
            var bus = provider.GetRequiredService<IMessageBus>();
            return (serverName, rootUri) => LSPSession.StartProcess(
                serverName,
                arguments: null,
                statusSink: msg => bus.Publish(new StatusMessage(msg)));
        });
        services.AddSingleton<DiagnosticStore>();
        // R8.3: Use a factory lambda to pass the debounce interval directly rather than
        // registering a bare TimeSpan singleton (which would collide with any future
        // service that also needs a TimeSpan injected from DI).
        services.AddSingleton<LSPManager>(provider => new LSPManager(
            provider.GetRequiredService<IMessageBus>(),
            provider.GetRequiredService<IDocumentManagementService>(),
            provider.GetRequiredService<ILanguageDetectionService>(),
            provider.GetRequiredService<DiagnosticStore>(),
            provider.GetRequiredService<Func<string, string?, LSPSession>>(),
            TimeSpan.FromMilliseconds(300)));

        // Phase 2 — File Explorer & Project System
        // IgnoreList has a public IEnumerable<string> constructor used by tests.
        // DI would prefer that constructor and pass an empty enumerable, so we
        // explicitly use the parameterless constructor that loads DefaultPatterns.
        services.AddSingleton<IIgnoreList>(_ => new IgnoreList());
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProjectLoader, ProjectLoader>();
        services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();

        // Phase 5 — Output panel (fake terminal)
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<FindReplaceViewModel>();
        services.AddSingleton<FileExplorerViewModel>();
        services.AddSingleton<ProblemsViewModel>();
        services.AddSingleton<OutputViewModel>();

        return services.BuildServiceProvider();
    }
}
