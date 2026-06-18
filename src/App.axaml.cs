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
        services.AddSingleton<DocumentManager>();

        // Phase 2 — File Explorer & Project System (M1: services only; M3: FileExplorerViewModel needs DocumentManager)
        // IgnoreList has a public IEnumerable<string> constructor used by tests.
        // DI would prefer that constructor and pass an empty enumerable, so we
        // explicitly use the parameterless constructor that loads DefaultPatterns.
        services.AddSingleton<IIgnoreList>(_ => new IgnoreList());
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProjectLoader, ProjectLoader>();
        services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<FindReplaceViewModel>();
        services.AddSingleton<FileExplorerViewModel>();

        return services.BuildServiceProvider();
    }
}
