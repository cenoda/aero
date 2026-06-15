# Core Infrastructure Patterns

## ObservableObject (MVVM base)

Base class for all ViewModels. Provides `INotifyPropertyChanged`:

```csharp
public abstract class ObservableObject : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

## RelayCommand (ICommand wrapper)

```csharp
public class RelayCommand : ICommand {
    readonly Action<object?> _execute;
    readonly Func<object?, bool>? _canExecute;
    // ... standard ICommand implementation
}
```

## MessageBus (Event Aggregator)

Decouples ViewModels/Services. Publish-subscribe without direct references:

```csharp
public class MessageBus {
    readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Subscribe<T>(Action<T> handler) { ... }
    public void Publish<T>(T message) { ... }
    public void Unsubscribe<T>(Action<T> handler) { ... }
}

// Message types (simple records)
public record DocumentOpened(TextDocument Document);
public record DocumentClosed(TextDocument Document);
public record ActiveDocumentChanged(TextDocument? Document);
public record BuildStarted(string Project);
public record BuildFinished(int ExitCode, string Output);
public record ThemeChanged(string ThemeName);
public record FolderOpened(string Path);
```

## Service Locator / DI

Simple manual DI for now (no heavy frameworks):

```csharp
public static class ServiceLocator {
    static readonly Dictionary<Type, object> _singletons = new();
    static readonly Dictionary<Type, Func<object>> _factories = new();

    public static void RegisterSingleton<T>(T instance) { ... }
    public static void Register<T>(Func<T> factory) { ... }
    public static T Get<T>() { ... }
}
```

Register in `Program.cs`:
```csharp
ServiceLocator.RegisterSingleton(new MessageBus());
ServiceLocator.RegisterSingleton<SettingsService>(() => new SettingsService());
ServiceLocator.RegisterSingleton<DocumentManager>(() => new DocumentManager(
    ServiceLocator.Get<MessageBus>()
));
```

## Startup Sequence

```
Program.Main()
    → BuildAvaloniaApp()
    → OnFrameworkInitializationCompleted()
        → Register all services in ServiceLocator
        → new ShellViewModel(ServiceLocator.Get<MessageBus>())
        → new MainWindow { DataContext = shellViewModel }
        → Show window
```
