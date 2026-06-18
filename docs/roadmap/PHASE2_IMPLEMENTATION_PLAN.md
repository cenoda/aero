# Phase 2 Detailed Implementation Plan — File Explorer & Project System

> This document breaks the high-level Phase 2 checklist from `PHASES.md` into concrete, implementable tasks. Each task lists the files to create/change, the expected behavior, and the tests that should accompany it.
>
> **Scope:** sidebar file tree, Open Folder, live refresh, ignore list, lazy loading, project-aware nodes, file operations, workspace persistence stub, and tests.

---

## 0. Scope & Non-Goals

### In Scope
- A single-root file explorer in the main window sidebar.
- Open Folder via menu and keyboard.
- Lazy, asynchronous tree loading with virtualization.
- Ignore list for directories such as `node_modules`, `bin`, `obj`, `.git`.
- Debounced `FileSystemWatcher` refresh.
- Read-only project detection for `.sln`, `.csproj`, and `package.json`.
- Context-menu file operations: New File, New Folder, Delete, Rename.
- Click a file in the tree to open it in the editor.
- Remember the last-opened folder and tree expansion state.
- Unit tests for all service-level logic.

### Out of Scope (handled later)
- Drag-and-drop in the tree.
- Multi-root workspaces.
- File content search / find-in-files.
- Git status overlays on file icons.
- Full solution build / project serialization.

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Views                                                        │
│  ├── MainWindow.axaml  ← sidebar + menu                       │
│  ├── FileExplorerView.axaml ← TreeView                        │
│  └── InputDialog.axaml  ← rename / new-name prompt            │
├─────────────────────────────────────────────────────────────┤
│  ViewModels                                                   │
│  ├── ShellViewModel  ← OpenFolder, layout, status             │
│  ├── FileExplorerViewModel  ← tree state & file ops           │
│  ├── FileTreeNodeViewModel  ← one node in the tree            │
│  └── EditorViewModel  ← already exists; handles open requests │
├─────────────────────────────────────────────────────────────┤
│  Services                                                     │
│  ├── IFileSystemService / FileSystemService                   │
│  ├── IIgnoreList / IgnoreList                                 │
│  ├── IFileSystemWatcherService / FileSystemWatcherService     │
│  ├── IProjectLoader / ProjectLoader                           │
│  └── IWorkspaceService / WorkspaceService                     │
├─────────────────────────────────────────────────────────────┤
│  Models                                                       │
│  ├── FileSystemNode  ← plain data                             │
│  └── ProjectNode     ← plain data                             │
├─────────────────────────────────────────────────────────────┤
│  Core                                                         │
│  └── Messages.cs  ← OpenDocumentRequest, FileSystemChanged    │
└─────────────────────────────────────────────────────────────┘
```

All new services are registered in `App.axaml.cs` and injected. Services never reference ViewModels; they communicate through `IMessageBus` records.

---

## 2. New Core Messages

Edit `src/Core/Messages.cs`.

| Record | Purpose |
|--------|---------|
| `OpenDocumentRequest(string FilePath)` | Published by the file tree when a file should be opened in the editor. `ShellViewModel` subscribes and routes it to `EditorViewModel.OpenFileAsync`. |
| `FileSystemChanged(string RootPath, string ChangedPath, FileSystemChangeKind Kind)` | Published by `FileSystemWatcherService` after debouncing. `FileExplorerViewModel` refreshes the affected subtree. |
| `StatusBarMessageRequested(string Text)` | Optional helper so services can surface errors without referencing the status bar directly. |

### 2.1 Enum `FileSystemChangeKind`
```csharp
public enum FileSystemChangeKind { Created, Deleted, Renamed, Changed }
```

---

## 3. Models

Create folder `src/Models/Workspace/`.

### 3.1 `src/Models/Workspace/FileSystemNode.cs`
- `string FullPath`
- `string Name`
- `FileSystemNodeKind Kind` (`File`, `Directory`)
- `IReadOnlyList<FileSystemNode> Children` (initially empty)
- Plain data object; no `INotifyPropertyChanged`, no logic.

### 3.2 `src/Models/Workspace/ProjectNode.cs`
- `string FullPath`
- `string Name`
- `ProjectKind Kind` (`Solution`, `CSharpProject`, `NodePackage`, `Unknown`)
- `IReadOnlyList<ProjectNode> Children`
- Plain data object.

### 3.3 `src/Models/Workspace/FileSystemNodeKind.cs`
```csharp
public enum FileSystemNodeKind { File, Directory }
```

### 3.4 `src/Models/Workspace/ProjectKind.cs`
```csharp
public enum ProjectKind { Solution, CSharpProject, NodePackage, Unknown }
```

---

## 4. Services

Create files in `src/Services/`.

### 4.1 `IIgnoreList` / `IgnoreList`

**Files:**
- `src/Services/IIgnoreList.cs`
- `src/Services/IgnoreList.cs`

**Behavior:**
- Default patterns: `node_modules`, `bin`, `obj`, `.git`, `.vs`, `packages`, `*.tmp`.
- `bool IsIgnored(string path, bool isDirectory)` — case-insensitive on Windows, case-sensitive elsewhere.
- Treat directory patterns as matching both the folder itself and anything inside it.
- Keep the implementation simple (name matching + limited glob) so it is unit-testable without a real disk.

**Acceptance:**
- `node_modules/foo/bar.cs` is ignored.
- `src/MyApp.cs` is not ignored.
- `bin/Debug/app.dll` is ignored.

**Tests:** `IgnoreListTests` covering defaults, directories vs files, and custom patterns.

---

### 4.2 `IFileSystemService` / `FileSystemService`

**Files:**
- `src/Services/IFileSystemService.cs`
- `src/Services/FileSystemService.cs`

**Behavior:**
- `IAsyncEnumerable<FileSystemNode> EnumerateDirectoryAsync(string path, IIgnoreList ignoreList, CancellationToken ct)`
- `Task CreateDirectoryAsync(string path)`
- `Task CreateFileAsync(string path)`
- `Task DeleteAsync(string path, bool recursive = false)`
- `Task RenameAsync(string oldPath, string newPath)`
- `bool Exists(string path)`
- All methods catch `UnauthorizedAccessException`, `DirectoryNotFoundException`, `IOException` and return empty/false or throw a domain exception such as `FileSystemException` with a friendly message.

**Acceptance:**
- Enumeration skips ignored entries.
- Large directories do not block; returns items as they are read.
- Errors are surfaced through return values or domain exceptions, not raw `System.IO` exceptions.

**Tests:** Create an in-memory test implementation of `IFileSystemService` so tree/ignore logic can be tested without touching disk.

---

### 4.3 `IFileSystemWatcherService` / `FileSystemWatcherService`

**Files:**
- `src/Services/IFileSystemWatcherService.cs`
- `src/Services/FileSystemWatcherService.cs`

**Behavior:**
- `void StartWatching(string path, IIgnoreList ignoreList)`
- `void StopWatching()`
- Internally uses `FileSystemWatcher`.
- Collects events into a short debounce window (default 300 ms).
- After the window expires, publishes `FileSystemChanged` messages on the UI thread via `Dispatcher.UIThread.InvokeAsync`.
- Ignore events whose paths match the ignore list.
- Coalesce duplicate events for the same path.

**Acceptance:**
- 50 rapid build-output events result in one or two refresh cycles, not 50.
- Events inside `node_modules` are dropped.
- Calling `StopWatching` disposes the watcher cleanly.

**Tests:** `FileSystemWatcherServiceTests` using a fake clock or observable to verify debounce and ignore logic without real file IO.

---

### 4.4 `IProjectLoader` / `ProjectLoader`

**Files:**
- `src/Services/IProjectLoader.cs`
- `src/Services/ProjectLoader.cs`

**Behavior:**
- `Task<IReadOnlyList<ProjectNode>> LoadProjectsAsync(string rootPath, IFileSystemService fileSystem, CancellationToken ct)`
- For a `.sln` file: parse `Project(...)` lines to extract project name and relative path, then create child `ProjectNode`s.
- For a `.csproj` file: create a single `ProjectNode` whose name comes from `<AssemblyName>` or the file name.
- For a `package.json` file: create a single `ProjectNode` whose name comes from `"name"` or the parent folder name.
- If none of the above exist, return an empty list; the tree will show the raw folder structure only.
- Read-only: never write to project files.

**Acceptance:**
- A folder with a `.sln` containing two `.csproj` files yields a solution node with two children.
- A folder with only a `package.json` yields one `NodePackage` node.
- Malformed project files do not crash; they are reported as `Unknown` or skipped.

**Tests:** `ProjectLoaderTests` with sample solution/project/package JSON strings.

---

### 4.5 `IWorkspaceService` / `WorkspaceService`

**Files:**
- `src/Services/IWorkspaceService.cs`
- `src/Services/WorkspaceService.cs`

**Behavior:**
- Persist workspace state to `~/.aero/workspace.json` (or `%LOCALAPPDATA%\Aero\workspace.json` on Windows).
- `Task SaveAsync(WorkspaceState state, CancellationToken ct)`
- `Task<WorkspaceState?> LoadAsync(CancellationToken ct)`
- `WorkspaceState` record: `string? LastOpenedFolder`, `IReadOnlyList<string> ExpandedPaths`, `string? SelectedPath`.
- Use `System.Text.Json`.

**Acceptance:**
- After opening `/home/user/code/aero` and expanding `src/Core`, restarting the app restores the folder and expansion.
- Corrupt workspace files are ignored silently; the app starts with no folder open.

**Tests:** `WorkspaceServiceTests` using a temporary directory for the state file.

---

## 5. ViewModels

### 5.1 `FileTreeNodeViewModel`

**File:** `src/ViewModels/FileTreeNodeViewModel.cs`

**Responsibilities:**
- Wrap one `FileSystemNode` for binding.
- Track `IsExpanded`, `IsSelected`, `IsEditingName`, `EditingName`.
- Expose lazy-loading `Children` collection.
- Provide `Kind` icon hint for the view (e.g., folder/file icon key).

**Behavior:**
- When `IsExpanded` changes to `true` and `Children` is empty, asynchronously load children via `IFileSystemService`.
- If the node represents a project root, merge `ProjectNode` children if available.
- Double-click / Enter on a file publishes `OpenDocumentRequest`.

**No direct View references.** All interaction is through commands and messages.

---

### 5.2 `FileExplorerViewModel`

**File:** `src/ViewModels/FileExplorerViewModel.cs`

**Responsibilities:**
- Own the root `FileTreeNodeViewModel` list.
- React to `FolderOpened` by loading a new root.
- React to `FileSystemChanged` by refreshing the affected subtree.
- Provide context-menu commands.

**Commands:**
- `OpenSelectedFileCommand`
- `NewFileCommand`
- `NewFolderCommand`
- `DeleteCommand`
- `RenameCommand`
- `RefreshCommand`

**Behavior:**
- On `FolderOpened(Path)`, load root nodes asynchronously, apply ignore list, load projects, and restore expansion/selection from `WorkspaceService`.
- On `FileSystemChanged`, locate the affected node and reload its parent directory.
- For `New File` / `New Folder`, create an in-place editable child node; on commit call `IFileSystemService` and refresh the parent.
- For `Rename`, set `IsEditingName` on the selected node; on commit call `IFileSystemService.RenameAsync`.
- For `Delete`, show a confirmation dialog via `InputDialog`, then call `DeleteAsync`.
- Surface errors through `StatusBarMessageRequested`.

**Acceptance:**
- Opening a folder shows files and directories within 100 ms for small folders.
- Ignored directories are never visible.
- External file creation appears in the tree after the debounce window.

---

### 5.3 `ShellViewModel` Changes

**File:** `src/ViewModels/ShellViewModel.cs`

**Changes:**
- Add `FileExplorerViewModel` property.
- Add `OpenFolderCommand` (menu + key binding `Ctrl+Shift+O`).
- Subscribe to `OpenDocumentRequest` and call `EditorViewModel.OpenFileAsync`.
- Subscribe to `StatusBarMessageRequested` and update `StatusText`.
- On folder open, publish `FolderOpened` and persist through `WorkspaceService`.
- On exit, save workspace state (expanded paths, selected path).

**Acceptance:**
- `File → Open Folder` opens a folder picker; on OK the tree is populated.
- Clicking a file in the tree opens a tab in the editor.

---

## 6. Views & Layout

### 6.1 `FileExplorerView.axaml`

**Files:**
- `src/Views/FileExplorerView.axaml`
- `src/Views/FileExplorerView.axaml.cs`

**Requirements:**
- `TreeView` with hierarchical data template.
- Each node shows a `MaterialIcon` (`Folder`, `FileDocument`, `CodeBraces`, `Nodejs`, etc.) and name.
- Editable header (TextBox) shown when `IsEditingName` is true.
- Context menu bound to `FileExplorerViewModel` commands.
- Minimal code-behind; logic lives in the ViewModel.

### 6.2 `InputDialog.axaml`

**Files:**
- `src/Views/InputDialog.axaml`
- `src/Views/InputDialog.axaml.cs`

**Requirements:**
- Simple dialog with a prompt label, text box, OK, and Cancel.
- Used for rename and new-file/new-folder name prompts.
- Avoids adding a dependency like `DialogHost.Avalonia` before Phase 8.

### 6.3 `MainWindow.axaml` Changes

**File:** `src/MainWindow.axaml`

**Changes:**
- Wrap the existing editor area in a two-column `Grid`:
  - Column 0: `FileExplorerView` with `Width="280"`, `IsVisible="{Binding IsFileExplorerVisible}"`.
  - Column 1: existing editor `Grid`.
- Add `Open Folder` to the `File` menu and key binding `Ctrl+Shift+O`.
- Keep the status bar as-is.

---

## 7. Dependency Injection Registration

Edit `src/App.axaml.cs`.

Add to `BuildServices`:

```csharp
// Core services
services.AddSingleton<IIgnoreList>(new IgnoreList());
services.AddSingleton<IFileSystemService, FileSystemService>();
services.AddSingleton<IFileSystemWatcherService, FileSystemWatcherService>();
services.AddSingleton<IProjectLoader, ProjectLoader>();
services.AddSingleton<IWorkspaceService, WorkspaceService>();

// ViewModels
services.AddSingleton<FileExplorerViewModel>();
```

`ShellViewModel` constructor should accept `FileExplorerViewModel`, `IWorkspaceService`, etc.

---

## 8. Tests

Create a test project `tests/Aero.Tests/Aero.Tests.csproj` if it does not exist.

### 8.1 Test Project Setup
- Framework: xUnit
- Mocking: NSubstitute
- Reference `src/aero.csproj` as a project reference.
- If no solution exists at repo root, create `aero.sln` and add both projects.

### 8.2 Unit Tests

| Test Class | What to Cover |
|------------|---------------|
| `IgnoreListTests` | Defaults, directory matching, file matching, custom patterns. |
| `FileSystemServiceTests` | Enumeration skips ignored entries; errors are handled. |
| `FileSystemWatcherServiceTests` | Debounce batches events; ignored paths dropped; stop/dispose safe. |
| `ProjectLoaderTests` | `.sln` parsing, `.csproj` parsing, `package.json` parsing, malformed input. |
| `WorkspaceServiceTests` | Save/load round-trip, corrupt file recovery. |
| `FileTreeNodeViewModelTests` | Expansion triggers load; open request published on file double-click. |
| `FileExplorerViewModelTests` | FolderOpened loads root; FileSystemChanged refreshes subtree. |

### 8.3 Integration Test (manual)
- Open a real C# solution folder in Aero.
- Delete/create files externally and confirm tree refresh.
- Verify ignored folders are hidden.

---

## 9. Recommended Implementation Order

1. **Models & messages** — `FileSystemNode`, `ProjectNode`, `OpenDocumentRequest`, `FileSystemChanged`.
2. **IgnoreList + tests** — foundational for every later service.
3. **FileSystemService + tests** — abstract disk access.
4. **ProjectLoader + tests** — read-only parsing.
5. **FileSystemWatcherService + tests** — debounced events.
6. **WorkspaceService + tests** — persistence stub.
7. **FileTreeNodeViewModel + FileExplorerViewModel** — build the tree logic.
8. **FileExplorerView + InputDialog** — bind the UI.
9. **ShellViewModel + MainWindow** — integrate sidebar, menu, and commands.
10. **DI registration** — wire everything in `App.axaml.cs`.
11. **Manual integration test + polish** — large-directory perf, status-bar errors, keyboard navigation.

---

## 10. Definition of Done

- `dotnet run --project src` starts and shows the file explorer sidebar.
- `File → Open Folder` opens a folder picker and populates the tree.
- Ignored directories are not shown.
- Clicking a file opens it in the editor.
- Context-menu New/Rename/Delete work with confirmation/input dialogs.
- External file changes refresh the tree after a short debounce.
- `.sln`, `.csproj`, and `package.json` are recognized and shown as project nodes.
- The last-opened folder and tree expansion are restored on restart.
- All new service logic has passing unit tests.
- `PHASES.md` Phase 2 checklist is updated as items are completed.

---

## 11. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Large folders (e.g., `node_modules`) freeze the UI | Ignore list + lazy async enumeration + virtualization. |
| Rapid build output overwhelms the tree | Debounced watcher with batching. |
| `FileSystemWatcher` misses events | Accept that watcher can miss events; provide manual **Refresh** command. |
| Cross-platform path issues | Use `System.IO.Path` everywhere; tests run on Linux and Windows. |
| Project parsing is fragile | Keep parsers minimal; malformed files become `Unknown` nodes instead of exceptions. |
