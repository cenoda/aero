# Core Infrastructure Patterns

> **Note**: This document covers infrastructure patterns. For abstraction-first design principles, see `AGENTS.md` Section 4.

## MVVM — Using ReactiveUI

All ViewModels inherit from ReactiveUI's `ReactiveObject`.
Do not create `ObservableObject` directly.

```csharp
// ViewModel example
public class FileExplorerViewModel : ReactiveObject {
    // [Reactive] attribute automatically enables change notification
    [Reactive] public string CurrentPath { get; set; } = "";

    // React to value changes with WhenAnyValue
    public FileExplorerViewModel() {
        this.WhenAnyValue(x => x.CurrentPath)
            .Subscribe(path => LoadFiles(path));
    }
}
```

Analogy — just as `[SerializeField]` makes a field appear in the Unity Inspector,
`[Reactive]` makes the UI automatically update when the value changes.

### Command (button clicks, etc.)

```csharp
public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

public MyViewModel() {
    OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
}
```

## DI — Using Microsoft.Extensions.DependencyInjection

Do not create a manual `ServiceLocator`.
Use the .NET standard DI container.

```csharp
// Program.cs — Service registration
var services = new ServiceCollection();

services.AddSingleton<IMessageBus, MessageBus>();
services.AddSingleton<DocumentManager>();
services.AddSingleton<IIgnoreList>(_ => new IgnoreList());
services.AddSingleton<IFileSystemService, FileSystemService>();
services.AddSingleton<IProjectLoader, ProjectLoader>();
services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();
services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();
services.AddSingleton<FileExplorerViewModel>();

var provider = services.BuildServiceProvider();
```

```csharp
// Constructor injection — just declare what you need; DI injects it automatically
public class DocumentManager {
    public DocumentManager(IMessageBus bus, SettingsService settings) {
        // DI injects this automatically
    }
}
```

## MessageBus (Event Bus)

ViewModels and Services communicate via messages without directly referencing each other.
Similar to Unity's `SendMessage()` or event system concept.

```csharp
public interface IMessageBus {
    void Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
    void Unsubscribe<T>(Action<T> handler);
}

// Message types (simple records)
public record DocumentOpened(TextDocument Document);
public record DocumentClosed(TextDocument Document);
public record ActiveDocumentChanged(TextDocument? Document);
public record BuildStarted(string Project);
public record BuildFinished(int ExitCode, string Output);
public record ThemeChanged(string ThemeName);
public record FolderOpened(string Path);
public record FolderChanged(string Path);
public record StatusMessage(string Text);
public record PromptNewItem(string ParentPath, bool IsFile, Action<string?> OnResult);
public record PromptRename(string Path, Action<string?> OnResult);
public record ConfirmDelete(string Path, Action<bool> OnResult);
```

`FolderOpened` is published by `ShellViewModel.OpenFolderCommand` (File → Open Folder / Ctrl+Shift+O)
and by an optional CLI startup-folder argument in `App.axaml.cs`. `FileExplorerViewModel` subscribes
and loads the workspace tree.

`FolderChanged` is published by `FileSystemWatcherService` after a debounced quiet period. It signals
that the watched workspace root may have changed on disk. `FileExplorerViewModel` subscribes and
refreshes the tree, marshalling the reload onto the UI thread.

`StatusMessage` is a transient status-bar / log message. `FileSystemWatcherService` publishes it when
the OS watcher fails (e.g. inotify limits, permissions) so the user sees a warning while manual refresh
remains available. `ShellViewModel` subscribes and updates `StatusText`.

## Startup Sequence

```
Program.Main()
    → BuildAvaloniaApp()
    → OnFrameworkInitializationCompleted()
        → Register services with ServiceCollection
        → provider.GetRequiredService<ShellViewModel>()
        → new MainWindow { DataContext = shellViewModel }
        → Show window
```

## ViewModel → Modal Dialog Bridge (MessageBus Subscriber)

ViewModels never open dialogs (MVVM). The pattern for asking the user a question
from a ViewModel — e.g. "Save / Don't Save / Cancel?" — is:

1. **Message** — define a record carrying a callback the user-facing layer invokes
   with the response:
   ```csharp
   public record ConfirmDirtyClose(string FileName, Action<string> OnResponse);
   ```
2. **Publish** — ViewModel calls `_bus.Publish(new ConfirmDirtyClose(...))`.
3. **Subscribe in code-behind** — `MainWindow` (the only place allowed to own a
   `Window` / show a dialog) gets `IMessageBus` via an explicit `Initialize(bus)`
   method called from `App.axaml.cs` after construction. Avalonia's XAML loader
   requires a parameterless ctor, so constructor injection on a `Window` is not
   available:
   ```csharp
   public MainWindow()
   {
       InitializeComponent();
   }

   public void Initialize(IMessageBus bus)
   {
       _bus = bus ?? throw new ArgumentNullException(nameof(bus));
       _bus.Subscribe<ConfirmDirtyClose>(OnConfirmDirtyClose);
   }

   private async void OnConfirmDirtyClose(ConfirmDirtyClose msg)
   {
       var result = await DirtyCloseDialog.ShowAsync(this, msg.FileName);
       msg.OnResponse(result ?? DirtyCloseResponse.Cancel);
   }
   ```
4. **Wire it up in `App.axaml.cs`** (resolve `IMessageBus` from the service
   provider, then call `Initialize`):
   ```csharp
   var bus = _services.GetRequiredService<IMessageBus>();
   var mainWindow = new MainWindow { DataContext = shell };
   mainWindow.Initialize(bus);
   desktop.MainWindow = mainWindow;
   ```

The dialog itself is a small `Window` subclass with code-behind (no ViewModel)
that returns the user's choice via `Close(response)`. See
`src/Views/DirtyCloseDialog.axaml` and `src/Views/DirtyCloseDialog.axaml.cs`.

---

## Phase 2 Service Registrations

The following services were added during Phase 2. All are registered as
**singletons** in
[`src/App.axaml.cs`](../../src/App.axaml.cs):

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `IgnoreList` | `IIgnoreList` | singleton | Pattern-based filtering of large/unwanted directories (`node_modules`, `bin`, `obj`, `.git`, `.vs`, `packages`, `*.tmp`). Custom code, no NuGet. Case-insensitive on Windows/macOS, case-sensitive on Linux. |
| `FileSystemService` | `IFileSystemService` | singleton | Async wrapper over `System.IO` for enumeration, create/rename/delete. Every method takes a `CancellationToken`. Paths normalized via `Path.GetFullPath()`. Filters results through `IIgnoreList`. |
| `ProjectLoader` | `IProjectLoader` | singleton | Extension-based recognition: `.sln`, `.csproj`, `package.json`. Read-only — does not modify project files. Full MSBuild / Node parsing deferred to Phase 6. |
| `FileSystemWatcherService` | `IFileSystemWatcherService` | singleton | Wraps `System.IO.FileSystemWatcher` with debouncing and `IIgnoreList` filtering. Watches one workspace root, publishes `FolderChanged` after a quiet period, and surfaces non-fatal watcher failures through `StatusMessage`. |
| `LanguageDetectionService` | `ILanguageDetectionService` | singleton | Extension-based language detection (case-insensitive). Maps file extensions to `LanguageInfo` records (e.g. `.cs` → C#, `.json` → JSON, `.csproj`/`.axaml`/`.xaml` → XML, `.md` → Markdown, plus common web/systems languages). Unknown or extension-less paths resolve to `LanguageInfo.PlainText`. UI-free, no Avalonia/TextMate types. Consumed by `DocumentManager` to set `TextDocument.Language` and by `EditorViewModel` to set `EditorTabViewModel.LanguageId`. |

**Models added:**
- `src/Models/Project/FileSystemEntry.cs` — plain record `{ Name, FullPath, Kind }`.
- `src/Models/Project/ProjectInfo.cs` — plain record `{ Path, Name, Kind }` where `Kind ∈ { None, Solution, CSharpProject, NodeProject }`.

**Consumer guidance:**
- ViewModels receive `IFileSystemService` and `IProjectLoader` via constructor injection; they never touch `System.IO` directly.
- Watcher consumers receive `IFileSystemWatcherService` via constructor injection; the service owns the underlying `FileSystemWatcher` lifetime and is disposed with the DI container.
- Always pass a `CancellationToken` to every I/O method — empty directories will not observe a pre-cancelled token without an upfront check.
- `IgnoreList.IsIgnored(path, isDirectory)` is the canonical "should this entry appear in the tree?" predicate.
