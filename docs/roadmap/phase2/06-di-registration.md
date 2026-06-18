# 6. Dependency Injection Registration

> **Parent:** [Phase 2 README](./README.md)
>
> **File to edit:** `src/App.axaml.cs`

---

## Registration Code

Add to the `BuildServices` method:

```csharp
// Core services
services.AddSingleton<IIgnoreList, IgnoreList>();
services.AddSingleton<IFileSystemService, FileSystemService>();
services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();
services.AddSingleton<IProjectLoader, ProjectLoader>();
services.AddSingleton<IWorkspaceService, WorkspaceService>();

// ViewModels
services.AddSingleton<FileExplorerViewModel>();
```

---

## Constructor Changes

`ShellViewModel` constructor should accept `FileExplorerViewModel`, `IWorkspaceService`, etc.

---

## Acceptance Criteria

- All five services are registered as singletons.
- `FileExplorerViewModel` is registered as a singleton.
- `ShellViewModel` receives its new dependencies via constructor injection.
- The app starts without DI resolution errors.