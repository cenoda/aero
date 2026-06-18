# ⛔ SUPERSEDED — 6. DI Registration

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative DI setup.
> Superseded: `async void OnDesktopExit` workspace save, `IIgnoreList` registration, `IWorkspaceService` registration.
> PROJECT_PLAN: no `OnDesktopExit` change, no `IIgnoreList`, no `IWorkspaceService`.

---

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

- `ShellViewModel` constructor should accept `FileExplorerViewModel`, `IWorkspaceService`, etc.
- `FileExplorerViewModel` constructor should accept `IFileSystemService`, `IProjectLoader`, `IFileSystemWatcherService`, `IIgnoreList`, `IWorkspaceService`, `IMessageBus`.

---

## App Exit — Workspace Save

**File:** `src/App.axaml.cs`

Modify `OnDesktopExit` to save workspace state before disposing the DI container:

```csharp
private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
{
    // Save workspace state before the container is torn down.
    // The container is still alive here, so all singletons are resolvable.
    var workspaceService = _services!.GetRequiredService<IWorkspaceService>();
    var shell = _services.GetRequiredService<ShellViewModel>();
    // Gather current state from ShellViewModel and persist
    await workspaceService.SaveAsync(shell.GetWorkspaceState(), CancellationToken.None);

    (_services as IDisposable)?.Dispose();
}
```

`ShellViewModel` must expose a `GetWorkspaceState()` method (or the save is triggered from `ShellViewModel`'s `Dispose()` — pick one and document it).

---

## Acceptance Criteria

- All five services are registered as singletons.
- `FileExplorerViewModel` is registered as a singleton.
- `ShellViewModel` and `FileExplorerViewModel` receive their new dependencies via constructor injection.
- Workspace state is persisted on app exit before the DI container disposes.
- The app starts without DI resolution errors.