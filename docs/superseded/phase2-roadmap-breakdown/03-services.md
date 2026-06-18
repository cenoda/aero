# ⛔ SUPERSEDED — 3. Services

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative service contracts.
> Superseded: `IIgnoreList`, `IFileSystemService.EnumerateDirectoryAsync`, `IFileSystemWatcherService` with ignore list, `IProjectLoader.LoadProjectsAsync`, `IWorkspaceService`.
> PROJECT_PLAN uses: `IFileSystemService.GetDirectoryEntriesAsync`, `IFileSystemWatcherService` without ignore list, `IProjectLoader.DetectProjectKind()` + `DetectProjects()`.

---

---

## 3.1 `IIgnoreList` / `IgnoreList`

**Files:**
- `src/Services/IIgnoreList.cs`
- `src/Services/IgnoreList.cs`

### Interface

```csharp
public interface IIgnoreList
{
    bool IsIgnored(string path, bool isDirectory);
    void AddPattern(string pattern); // for user-defined patterns (settings in Phase 8)
}
```

### Behavior
- Default patterns: `node_modules`, `bin`, `obj`, `.git`, `.vs`, `packages`, `*.tmp`.
- `AddPattern` appends user-defined patterns. Patterns added later are evaluated after defaults.
- Case-insensitive on Windows (`OperatingSystem.IsWindows()`), case-sensitive elsewhere. Detect at construction time.
- Treat directory patterns as matching both the folder itself and anything inside it.
- Keep the implementation simple (name matching + limited glob) so it is unit-testable without a real disk.

### Acceptance
- `node_modules/foo/bar.cs` is ignored.
- `src/MyApp.cs` is not ignored.
- `bin/Debug/app.dll` is ignored.

### Tests
`IgnoreListTests` covering defaults, directories vs files, and custom patterns.

---

## 3.2 `IFileSystemService` / `FileSystemService`

**Files:**
- `src/Services/IFileSystemService.cs`
- `src/Services/FileSystemService.cs`

### Interface

```csharp
public interface IFileSystemService
{
    IAsyncEnumerable<FileSystemNode> EnumerateDirectoryAsync(string path, IIgnoreList ignoreList, CancellationToken ct);
    Task CreateDirectoryAsync(string path, CancellationToken ct);
    Task CreateFileAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    Task RenameAsync(string oldPath, string newPath, CancellationToken ct);
    bool Exists(string path);
}
```

### Behavior
- `EnumerateDirectoryAsync` returns items as they are read (streaming).
- All methods catch `UnauthorizedAccessException`, `DirectoryNotFoundException`, `IOException` and return empty/false or throw a domain exception such as `FileSystemException` with a friendly message.
- Symlinks/junctions: do NOT follow directory symlinks to avoid infinite loops. Treat symlinked directories as leaf nodes with a special icon hint (`FileSystemNodeKind.Symlink` or a `bool IsSymlink` flag in `FileSystemNode`).

### Acceptance
- Enumeration skips ignored entries.
- Large directories do not block; returns items as they are read.
- Errors are surfaced through return values or domain exceptions, not raw `System.IO` exceptions.

### Tests
Create an in-memory test implementation of `IFileSystemService` so tree/ignore logic can be tested without touching disk.

---

## 3.3 `IFileSystemWatcherService` / `FileSystemWatcherService`

**Files:**
- `src/Services/IFileSystemWatcherService.cs`
- `src/Services/FileSystemWatcherService.cs`

### Interface

```csharp
public interface IFileSystemWatcherService : IDisposable
{
    void StartWatching(string path, IIgnoreList ignoreList);
    void StopWatching();
}
```

### Behavior
- Internally uses `FileSystemWatcher`.
- Collects events into a short debounce window (default 300 ms).
- After the window expires, publishes `FileSystemChanged` messages on the UI thread via `Dispatcher.UIThread.InvokeAsync`.
- Ignore events whose paths match the ignore list.
- Coalesce duplicate events for the same path.

### Acceptance
- 50 rapid build-output events result in one or two refresh cycles, not 50.
- Events inside `node_modules` are dropped.
- Calling `StopWatching` disposes the watcher cleanly.

### Tests
`FileSystemWatcherServiceTests` using a fake clock or observable to verify debounce and ignore logic without real file IO.

---

## 3.4 `IProjectLoader` / `ProjectLoader`

**Files:**
- `src/Services/IProjectLoader.cs`
- `src/Services/ProjectLoader.cs`

### Interface

```csharp
public interface IProjectLoader
{
    Task<IReadOnlyList<ProjectNode>> LoadProjectsAsync(string rootPath, CancellationToken ct);
}
```

### Behavior
- For a `.sln` file: parse `Project(...)` lines to extract project name and relative path, then create child `ProjectNode`s.
- For a `.csproj` file: create a single `ProjectNode` whose name comes from `<AssemblyName>` or the file name.
- For a `package.json` file: create a single `ProjectNode` whose name comes from `"name"` or the parent folder name.
- If none of the above exist, return an empty list; the tree will show the raw folder structure only.
- Read-only: never write to project files.
- `ProjectLoader` should receive `IFileSystemService` via constructor injection (DI), not as a method parameter.

### Acceptance
- A folder with a `.sln` containing two `.csproj` files yields a solution node with two children.
- A folder with only a `package.json` yields one `NodePackage` node.
- Malformed project files do not crash; they are reported as `Unknown` or skipped.

### Tests
`ProjectLoaderTests` with sample solution/project/package JSON strings.

---

## 3.5 `IWorkspaceService` / `WorkspaceService`

**Files:**
- `src/Services/IWorkspaceService.cs`
- `src/Services/WorkspaceService.cs`

### Interface

```csharp
public interface IWorkspaceService
{
    Task SaveAsync(WorkspaceState state, CancellationToken ct);
    Task<WorkspaceState?> LoadAsync(CancellationToken ct);
}
```

### `WorkspaceState` Record

**File:** `src/Models/Workspace/WorkspaceState.cs`

```csharp
namespace Aero.Models.Workspace;

public record WorkspaceState(
    string? LastOpenedFolder,
    IReadOnlyList<string> ExpandedPaths,
    string? SelectedPath
);
```

### Behavior
- Persist workspace state to `~/.aero/workspace.json` (or `%LOCALAPPDATA%\Aero\workspace.json` on Windows).
- Use `System.Text.Json`.

### Acceptance
- After opening `/home/user/code/aero` and expanding `src/Core`, restarting the app restores the folder and expansion.
- Corrupt workspace files are ignored silently; the app starts with no folder open.

### Tests
`WorkspaceServiceTests` using a temporary directory for the state file.