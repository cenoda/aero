using System;
using System.Linq;
using Aero.Core;
using Aero.Languages;
using Aero.Services;
using Aero.Services.Build;
using Aero.Services.Git;
using Aero.Terminal;
using Aero.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dock.Avalonia.Themes.Simple;
using Microsoft.Extensions.DependencyInjection;

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
            // Phase 8.2 — Theme Engine: wire up presets and apply persisted theme
            var themeService = _services.GetRequiredService<ThemeService>();
            themeService.WireThemeDictionaries();
            _ = themeService.ApplyThemeAsync();

            // Phase 8.1a: Add Dock theme programmatically (ControlTheme, not ResourceDictionary)
            Styles.Add(new DockSimpleTheme());

            var shell = _services.GetRequiredService<ShellViewModel>();
            var bus = _services.GetRequiredService<IMessageBus>();

            // Eagerly resolve LSPManager so it subscribes to FolderOpened before any
            // folder is opened and is disposed with the DI container on app exit.
            _services.GetRequiredService<LSPManager>();

            // Eagerly resolve GitViewModel so it subscribes to FolderOpened before any
            // folder is opened (Phase 7).
            _services.GetRequiredService<GitViewModel>();

            var mainWindow = new MainWindow { DataContext = shell };
            mainWindow.Initialize(bus);
            desktop.MainWindow = mainWindow;

            // CLI args take precedence over workspace restore
            if (desktop.Args is { Length: > 0 } args && System.IO.Directory.Exists(args[0]))
            {
                bus.Publish(new FolderOpened(System.IO.Path.GetFullPath(args[0])));
            }
            else
            {
                // Workspace restore — skip when CLI arg is present
                _ = RestoreWorkspaceAsync(shell, mainWindow, bus);
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

    private async System.Threading.Tasks.Task RestoreWorkspaceAsync(
        ShellViewModel shell,
        MainWindow mainWindow,
        IMessageBus bus)
    {
        try
        {
            var settings = _services?.GetRequiredService<ISettingsService>();
            if (settings == null) return;

            var ws = await settings.LoadWorkspaceStateAsync();

            if (ws?.Window is { } win)
            {
                shell.WindowWidth = win.Width;
                shell.WindowHeight = win.Height;
                shell.IsWindowMaximized = win.IsMaximized;
                mainWindow.Position = new Avalonia.PixelPoint((int)win.X, (int)win.Y);
                mainWindow.Width = win.Width;
                mainWindow.Height = win.Height;
                mainWindow.WindowState = win.IsMaximized
                    ? Avalonia.Controls.WindowState.Maximized
                    : Avalonia.Controls.WindowState.Normal;
            }

            if (ws?.LastFolderPath is { } folder && System.IO.Directory.Exists(folder))
            {
                bus.Publish(new FolderOpened(folder));

                foreach (var fp in ws.OpenFilePaths.Where(System.IO.File.Exists))
                {
                    try { await shell.EditorViewModel.OpenFileAsync(fp); }
                    catch (Exception ex)
                    {
                        bus.Publish(new StatusMessage(
                            $"Failed to restore {fp}: {ex.Message}"));
                    }
                }

                if (ws.ActiveTabIndex >= 0
                    && ws.ActiveTabIndex < shell.EditorViewModel.Tabs.Count)
                    shell.EditorViewModel.ActivateTab(
                        shell.EditorViewModel.Tabs[ws.ActiveTabIndex]);
            }
        }
        catch (Exception ex)
        {
            bus.Publish(new StatusMessage(
                $"Workspace restore failed: {ex.Message}"));
        }
    }

    public static ServiceProvider BuildServices()
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

        // Phase 6 — Build system
        services.AddSingleton<DotNetBuildService>();
        services.AddSingleton<BuildServiceFactory>();

        // Phase 7 — Git integration
        services.AddSingleton<GitServiceFactory>();

        // Phase 8.7 — Workspace persistence & settings
        services.AddSingleton<ISettingsService, SettingsService>();

        // Phase 8.2 — Theme engine
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<FindReplaceViewModel>();
        services.AddSingleton<FileExplorerViewModel>();
        services.AddSingleton<ProblemsViewModel>();
        services.AddSingleton<OutputViewModel>();
        services.AddSingleton<GitViewModel>();

        return services.BuildServiceProvider();
    }
}
