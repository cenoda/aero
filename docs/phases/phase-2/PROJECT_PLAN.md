# Phase 2: File Explorer & Project System — Implementation Plan

> **Goal:** Add a workspace-quality file explorer and lightweight project recognition while keeping the Phase 1 editor rock-solid.

---

## 1. Entry Gate: Confirm Phase 1 Is Solid Before Crossing the Boundary

Do not write Phase 2 code until all of these are true:

| Gate | Evidence |
|------|----------|
| `docs/roadmap/PHASES.md` Phase 1 checklist all `[x]` | ✅ |
| `docs/phases/phase-1/TOFIX.md` all closed (Rounds 1–4) | ✅ |
| `dotnet build src` succeeds with 0 errors / 0 warnings | ✅ |
| `dotnet test tests` passes (89/89) | ✅ |
| `./manual_test_phase1.sh` smoke test completes | ✅ |
| `docs/issues/INDEX.md` has no open blockers | ✅ |

**First Phase 2 file to create:** `docs/phases/phase-2/TOFIX.md` from the template, so every review finding has a home before code lands.

---

## 2. Scope

### In Scope

- Left sidebar file explorer panel with a `TreeView`.
- `File → Open Folder` using Avalonia's folder picker.
- Tree nodes: directories and files, with Material icons.
- Pattern-based ignore list (`node_modules`, `bin`, `obj`, `.git`, `.vs`, `packages`) — eager loading means large ignore targets freeze the UI without this. Custom code, not a NuGet dependency (satisfies ADR-7).
- Lightweight project recognition for `.sln`, `.csproj`, `package.json`.
- Double-click a file → open in editor (reuse `DocumentManager`).
- Context menu: **New File**, **New Folder**, **Rename**, **Delete**.
- `FileSystemWatcher` with debounced auto-refresh.
- Manual refresh button/menu item.

### Out of Scope (protects solid state)

- Drag-and-drop docking (`Dock.Avalonia` was reserved for Phase 8, but abandoned 2026-06-25 — fixed Grid layout is permanent).
- Full MSBuild / solution parsing.
- Git status icons or file-modified badges in the tree.
- Find-in-files, file content search.
- Lazy directory loading (Phase 2 ships eager load; lazy load is a documented future optimization).
- Language-server integration.

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        MainWindow                            │
│  ┌──────────────┬──────┬─────────────────────────────────┐  │
│  │ FileExplorer │Grid  │ EditorView (tabs + welcome)     │  │
│  │  (TreeView)  │Split-│                                 │  │
│  │              │ter   │                                 │  │
│  └──────────────┴──────┴─────────────────────────────────┘  │
│  └────────────────────── StatusBar ────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

Services (new)              ViewModels (new)           Views (new)
─────────────────────────────────────────────────────────────────────
IFileSystemService          FileExplorerViewModel      FileExplorerView
IFileSystemWatcherService   FileExplorerNodeViewModel  TextInputDialog
IProjectLoader                                         ConfirmDialog
IIgnoreList
IgnoreList

MessageBus (add to src/Core/Messages.cs)
─────────────────────────────────────────────────────────────────────
FolderChanged(string Path)
PromptNewItem(string ParentPath, bool IsFile, Action<string?> OnResult)
PromptRename(string Path, Action<string?> OnResult)
ConfirmDelete(string Path, Action<bool> OnResult)
```

`DocumentManager` and `EditorViewModel` are **not** rewritten. `FileExplorerViewModel` calls `_documentManager.OpenDocumentAsync(path)` to open files, and the existing `EditorViewModel.OnDocumentOpened` handler guarantees the tab is created.

---

## 4. Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| ADR-1 | Introduce `IFileSystemService` abstraction over `System.IO` | Testability, centralized exception handling, cross-platform path safety. |
| ADR-2 | Tree models are plain records; `ObservableCollection` lives in VM layer | Keeps models simple and UI-agnostic; matches `CONVENTIONS.md` MVVM rules. |
| ADR-3 | `FileSystemWatcherService` is a singleton that publishes `FolderChanged` | Decouples OS watcher from UI; VM decides how to refresh and on which thread. |
| ADR-4 | Dialog prompts use the existing MessageBus pattern (`ConfirmDirtyClose`) | ViewModels never reference Views; `MainWindow` code-behind owns all dialogs. |
| ADR-5 | Sidebar uses manual `Grid` + `GridSplitter` | `PANELS_AND_DOCKING.md` recommends this for Phase 1–2; Dock.Avalonia was abandoned 2026-06-25, fixed Grid layout is permanent. |
| ADR-6 | `ProjectLoader` is recognition-only, not parsing | Meets Phase 2 exit condition without pulling in MSBuild/XML parsers. |
| ADR-7 | No new NuGet packages | Built-in `FileSystemWatcher` + already referenced `Material.Icons.Avalonia` are sufficient. |

---

## 5. Component Design

### 5.1 Models

```csharp
// src/Models/Project/FileSystemEntry.cs
public enum FileSystemEntryKind { Directory, File }

public record FileSystemEntry(
    string Name,
    string FullPath,
    FileSystemEntryKind Kind);
```

```csharp
// src/Models/Project/ProjectInfo.cs
public enum ProjectKind { None, Solution, CSharpProject, NodeProject }

public record ProjectInfo(
    string Path,
    string Name,
    ProjectKind Kind);
```

### 5.2 Services

#### `IIgnoreList` / `IgnoreList`

```csharp
// src/Services/IIgnoreList.cs
public interface IIgnoreList
{
    bool IsIgnored(string path, bool isDirectory);
    void AddPattern(string pattern);
}
```

```csharp
// src/Services/IgnoreList.cs
// Default patterns: "node_modules", "bin", "obj", ".git", ".vs", "packages", "*.tmp".
// Case-insensitive on Windows (OperatingSystem.IsWindows()), case-sensitive elsewhere.
// Directory patterns match both the folder AND anything inside it.
// Simple name/prefix matching; no full glob. Unit-testable without real disk.
```

**Why it's here despite ADR-7:** Eager loading + large ignored directories = UI freeze. This is custom code (~60 lines), no NuGet package, so ADR-7 is satisfied. `FileSystemService` filters `GetDirectoryEntriesAsync` through `IIgnoreList` before returning entries. The watcher also skips ignored paths.

#### `IFileSystemService` / `FileSystemService`

```csharp
// src/Services/IFileSystemService.cs
public interface IFileSystemService
{
    Task<IReadOnlyList<FileSystemEntry>> GetDirectoryEntriesAsync(
        string path, CancellationToken ct = default);

    Task CreateFileAsync(string parentPath, string name, CancellationToken ct = default);
    Task CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default);
    Task RenameAsync(string path, string newName, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
}
```

```csharp
// src/Services/IFileSystemWatcherService.cs
public interface IFileSystemWatcherService : IDisposable
{
    void Watch(string path);
    void StopWatching();
    bool IsWatching { get; }
}
```

**`FileSystemWatcherService` implementation notes:**
- Wraps `System.IO.FileSystemWatcher` with `IncludeSubdirectories = true`.
- Debounces events: after the last event, wait **300 ms**, then publish `FolderChanged(path)`.
- Handles the `Error` event by stopping the watcher, logging, and surfacing a status warning. Manual refresh remains available.
- `Watch(path)` stops any previous watcher first; there is only one active workspace folder at a time.
- Filters out events whose paths match `IIgnoreList.IsIgnored()`.

```csharp
// src/Services/IProjectLoader.cs
public interface IProjectLoader
{
    ProjectKind DetectProjectKind(string path);
    IReadOnlyList<ProjectInfo> DetectProjects(string rootPath, CancellationToken ct = default);
}
```

**`ProjectLoader` implementation notes:**
- `DetectProjectKind` maps extension: `.sln` → `Solution`, `.csproj` → `CSharpProject`, `package.json` → `NodeProject`, else `None`.
- `DetectProjects` enumerates the **workspace root only** (one level deep) and returns project files directly under it. This keeps the call cheap even for huge trees that already escaped the ignore list (e.g. a stray top-level `Cargo.toml` next to `node_modules`). The plan originally said "root and subdirectories"; that was rolled back to one level in M1 review to avoid deep traversal cost. Used by the tree to highlight solution/project roots with project-specific icons; not required to parse contents.

### 5.3 ViewModels

```csharp
// src/ViewModels/FileExplorerNodeViewModel.cs
public class FileExplorerNodeViewModel : ReactiveObject
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string IconKind { get; }   // e.g. "Folder", "File", "FileCode", "Solution", "Package"
    public ObservableCollection<FileExplorerNodeViewModel> Children { get; } = new();

    [Reactive] public bool IsExpanded { get; set; }
}
```

```csharp
// src/ViewModels/FileExplorerViewModel.cs
public class FileExplorerViewModel : ReactiveObject, IDisposable
{
    [Reactive] public string? RootPath { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public FileExplorerNodeViewModel? SelectedNode { get; set; }

    public ObservableCollection<FileExplorerNodeViewModel> RootNodes { get; } = new();

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSelectedFileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public async Task LoadFolderAsync(string path, CancellationToken ct = default);
}
```

**Responsibilities:**
- Subscribe to `FolderOpened` and `FolderChanged`; trigger `LoadFolderAsync`.
- Cancel any in-flight load via a stored `CancellationTokenSource` before starting a new one.
- Build the node tree off the UI thread (`Task.Run`) and replace `RootNodes` on the UI thread.
- For file activation, call `_documentManager.OpenDocumentAsync(selected.FullPath)`.
- For context-menu actions, publish `PromptNewItem`, `PromptRename`, `ConfirmDelete` and act on the callback.

### 5.4 Views

- **`FileExplorerView.axaml`** — `TreeView` with a `TreeDataTemplate` binding `Children`, `ContextMenu` bound to VM commands, `MaterialIcon` for icons. Keyboard accessibility: Arrow keys navigate, Enter opens file, Delete triggers delete, F2 triggers rename.
- **`TextInputDialog.axaml`** — reusable text prompt (OK/Cancel), used for New and Rename.
- **`ConfirmDialog.axaml`** — reusable yes/no confirmation, used for Delete.
- **`MainWindow.axaml`** — restructure the content area into a 3-column `Grid`: sidebar (250 px), `GridSplitter`, editor. Keep the existing menu and status bar untouched. NOTE: This fixed Grid layout is permanent — Dock.Avalonia was abandoned 2026-06-25.

### 5.5 Messages

Add to `src/Core/Messages.cs`:

```csharp
public record FolderChanged(string Path);

public record PromptNewItem(
    string ParentPath,
    bool IsFile,
    Action<string?> OnResult);

public record PromptRename(
    string Path,
    Action<string?> OnResult);

public record ConfirmDelete(
    string Path,
    Action<bool> OnResult);
```

`MainWindow.axaml.cs` subscribes to these and shows the small dialog windows, exactly like it already does for `ConfirmDirtyClose`.

---

## 6. File & Folder Layout

| Path | Action | Purpose |
|------|--------|---------|
| `src/Models/Project/FileSystemEntry.cs` | new | Plain tree node model |
| `src/Models/Project/ProjectInfo.cs` | new | Project recognition model |
| `src/Services/IFileSystemService.cs` | new | File I/O abstraction |
| `src/Services/FileSystemService.cs` | new | File I/O implementation |
| `src/Services/IFileSystemWatcherService.cs` | new | Watcher abstraction |
| `src/Services/FileSystemWatcherService.cs` | new | Debounced watcher implementation |
| `src/Services/IIgnoreList.cs` | new | Ignore list interface |
| `src/Services/IgnoreList.cs` | new | Ignore list implementation |
| `src/Services/IProjectLoader.cs` | new | Project recognition abstraction |
| `src/Services/ProjectLoader.cs` | new | Project recognition implementation |
| `src/ViewModels/FileExplorerViewModel.cs` | new | Explorer panel VM |
| `src/ViewModels/FileExplorerNodeViewModel.cs` | new | Tree node VM |
| `src/Views/FileExplorerView.axaml` | new | Explorer panel view |
| `src/Views/FileExplorerView.axaml.cs` | new | View code-behind (tree events) |
| `src/Views/TextInputDialog.axaml` | new | New/Rename dialog |
| `src/Views/TextInputDialog.axaml.cs` | new | Dialog code-behind |
| `src/Views/ConfirmDialog.axaml` | new | Delete confirmation dialog |
| `src/Views/ConfirmDialog.axaml.cs` | new | Dialog code-behind |
| `src/ViewModels/ShellViewModel.cs` | modify | Add `OpenFolderCommand`, `FileExplorerViewModel` property |
| `src/MainWindow.axaml` | modify | Sidebar + GridSplitter layout |
| `src/MainWindow.axaml.cs` | modify | Subscribe prompt messages |
| `src/Core/Messages.cs` | modify | Add new MessageBus records |
| `src/App.axaml.cs` | modify | Register new services/VMs |
| `tests/Stubs/MockFileSystemService.cs` | new | In-memory file system for VM tests |
| `tests/Stubs/MockFileSystemWatcherService.cs` | new | Testable watcher stub |
| `tests/Services/FileSystemServiceTests.cs` | new | Real temp-dir tests |
| `tests/Services/IgnoreListTests.cs` | new | Pattern matching tests |
| `tests/Services/ProjectLoaderTests.cs` | new | Recognition tests |
| `tests/Services/FileSystemWatcherServiceTests.cs` | new | Debounce/integration tests |
| `tests/ViewModels/FileExplorerViewModelTests.cs` | new | VM behavior tests |
| `docs/phases/phase-2/TOFIX.md` | new | Phase 2 quality checklist |
| `docs/phases/phase-2/PROJECT_PLAN.md` | new | Committed copy of this plan |
| `docs/roadmap/PHASES.md` | modify | Mark Phase 2 items `[x]` |

---

## 7. Milestone Plan (Solid-State Sprints)

Each milestone ends with **`dotnet build src` + `dotnet test tests` + a 30-second `dotnet run` smoke test**. If a gate fails, fix before continuing.

### M0 — Entry Gate
- Verify Phase 1 gates from §1.
- Create `docs/phases/phase-2/TOFIX.md`.
- Create empty new files/folders so the project compiles at each step.

### M1 — File System Abstraction & Project Recognition
- Implement `IIgnoreList`, `IgnoreList`, `IFileSystemService`, `FileSystemService`, `IProjectLoader`, `ProjectLoader`, and models.
- Add unit tests using temp directories.
- **Gate:** `IgnoreListTests`, `FileSystemServiceTests`, and `ProjectLoaderTests` pass.

### M2 — Tree ViewModel & Panel UI
- Implement `FileExplorerNodeViewModel` and `FileExplorerViewModel`.
- Create `FileExplorerView.axaml` with `TreeView` and icons.
- Restructure `MainWindow.axaml` to host the sidebar.
- Add `FileExplorerViewModel` to DI and `ShellViewModel`.
- **Gate:** App launches; sidebar is visible and empty; no Phase 1 layout regression.

### M3 — Open Folder & File Activation
- Add `OpenFolderCommand` to `ShellViewModel` using Avalonia folder picker.
- Publish `FolderOpened` and load the tree in `FileExplorerViewModel`.
- Wire double-click / Enter on a file node to `_documentManager.OpenDocumentAsync`.
- **Gate:** `File → Open Folder` populates the tree; double-clicking a file opens a tab.

### M4 — Context Menu Operations
- Add context menu commands: New File, New Folder, Rename, Delete.
- Publish `PromptNewItem`, `PromptRename`, `ConfirmDelete` from the VM.
- Implement `TextInputDialog` and `ConfirmDialog`; subscribe in `MainWindow.axaml.cs`.
- Validate names (not empty, no invalid chars, no collisions).
- Refresh the parent node after each operation.
- **Gate:** All four operations work and surface errors gracefully.

### M5 — FileSystemWatcher & Auto-Refresh
- Implement `FileSystemWatcherService` with debounce.
- Start watching on `FolderOpened`; stop on folder change / app exit.
- Subscribe `FileExplorerViewModel` to `FolderChanged` and refresh.
- Add manual Refresh command/button.
- **Gate:** External file create/rename/delete is reflected within ~500 ms.

### M6 — Exit Gate
- Full regression: Phase 1 editor features still pass automated + manual tests.
- Fill `docs/phases/phase-2/TOFIX.md` with any findings and close them.
- Update `docs/roadmap/PHASES.md`, `docs/architecture/CORE_INFRASTRUCTURE.md`.
- Create `manual_test_phase2.sh` and run it.
- **Gate:** `dotnet test` passes, app runs, Phase 2 checklist complete.

---

## 8. Testing Strategy

### 8.1 Unit Tests with Stubs

- `MockFileSystemService` — in-memory file tree. Supports create/rename/delete/exists and returns deterministic `FileSystemEntry` lists.
- `MockFileSystemWatcherService` — test double that lets tests raise `FolderChanged` manually.
- Existing `StubMessageBus` records published messages.

`FileExplorerViewModelTests` will use:
- `MockFileSystemService` + `ProjectLoader` + `DocumentManager` + `StubMessageBus`.
- Assert that `LoadFolderAsync` builds the right tree.
- Assert that opening a selected file adds a document to `DocumentManager`.
- Assert that New/Rename/Delete commands publish the correct prompt messages.

### 8.2 Integration Tests

- `FileSystemServiceTests` — temp directory create/read/rename/delete.
- `ProjectLoaderTests` — create temp `.sln`, `.csproj`, `package.json`, `foo.txt`; assert detected kinds.
- `FileSystemWatcherServiceTests` — create temp dir, start watcher, create a file, assert `FolderChanged` is published after debounce (use a small debounce timeout in tests).

### 8.3 Regression Tests

- After every milestone, run the full `dotnet test tests` suite. The existing 89 Phase 1 tests must continue to pass unchanged.
- Keep `DocumentManager` and `EditorViewModel` public surfaces stable; no test rewrites for Phase 1 code.

### 8.4 Manual Smoke Test

Create `manual_test_phase2.sh` (style matches Phase 1) that:
1. Launches Aero under Xvfb.
2. Sends `Ctrl+Shift+O` to open a folder picker (or uses a CLI argument if added).
3. Verifies the sidebar tree appears.
4. Sends a synthetic external file create and verifies tree refresh.
5. Verifies that double-clicking a file opens a tab.

---

## 9. Solid-State Safeguards

| Safeguard | How it is enforced |
|-----------|-------------------|
| **Additive changes only** | `DocumentManager`, `EditorViewModel`, `TextDocument`, and existing messages are not modified except for required new message subscriptions. |
| **Cancellation on all I/O** | Every `IFileSystemService` method takes `CancellationToken`; `FileExplorerViewModel` cancels the previous load before starting a new one. |
| **Dispose pattern** | `FileSystemWatcherService`, `FileExplorerViewModel`, and new dialogs implement `IDisposable`; `App.axaml.cs` already disposes the DI container on exit. |
| **Exception isolation** | I/O exceptions are caught in the service/VM and surfaced via `ErrorMessage` or `StatusText`; they do not escape command handlers. |
| **UI-thread discipline** | Services stay UI-agnostic; only the VM marshals collection replacement to the UI thread when necessary. |
| **Path safety** | All path construction uses `Path.Combine`, `Path.GetFileName`, `Path.GetDirectoryName`. No string concatenation of paths. |
| **Watcher fallback** | If `FileSystemWatcher` errors (inotify limits, permissions), auto-refresh stops gracefully and the user sees a status warning; manual refresh still works. |
| **Name validation** | New/Rename validates empty names, invalid path chars, and existing collisions before touching disk. |
| **No new dependencies** | Uses built-in `FileSystemWatcher` and already referenced `Material.Icons.Avalonia`. No surprise NuGet additions. |
| **No async void** | Async commands use `ReactiveCommand.CreateFromTask`; fire-and-forget bus handlers use `async Task` helpers assigned to discard with try/catch. |

---

## 10. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| `FileSystemWatcher` hits Linux `inotify` watch limits | Medium | Auto-refresh breaks | Catch `Error`, show status, keep manual refresh. |
| Large workspace causes UI freeze during tree build | Low-Medium | Poor UX | Async enumeration with cancellation; consider lazy load in a follow-up. |
| Renaming/deleting a file that is open in the editor | Medium | Tab points to stale path | Document as known limitation in TOFIX; do not silently corrupt. |
| Cross-platform path separator bugs | Low | Crashes on Windows/Mac | Use `Path` APIs exclusively. |
| Rapid folder switches create race conditions | Low | Wrong tree shown | Cancel previous load, serialize via `CancellationTokenSource`. |
| Phase 1 editor layout breaks when sidebar is added | Low | Regression | Keep menu/status bar untouched; only restructure the content grid. |

---

## 11. Documentation & Commit Plan

### Docs to Update

- `docs/phases/phase-2/TOFIX.md` — create at the start, keep empty until review findings appear.
- `docs/phases/phase-2/PROJECT_PLAN.md` — commit a copy of this plan.
- `docs/roadmap/PHASES.md` — mark Phase 2 checklist items `[x]` as milestones land.
- `docs/architecture/CORE_INFRASTRUCTURE.md` — document new MessageBus records and DI registrations.
- `docs/LIBRARIES.md` — no changes needed (no new packages).

### Suggested Commit Sequence

```
filesystem: add IFileSystemService, IProjectLoader, and project models
explorer: add FileExplorerViewModel and FileExplorerNodeViewModel
ui: add FileExplorerView, TextInputDialog, ConfirmDialog
shell: integrate sidebar with GridSplitter and Open Folder command
explorer: add context menu New/Rename/Delete
explorer: add FileSystemWatcherService with debounced auto-refresh
docs: update Phase 2 roadmap, architecture docs, and TOFIX
```

---

## 12. Exit Criteria

Phase 2 is complete when **all** of the following are true:

- [ ] `docs/roadmap/PHASES.md` Phase 2 checklist is fully `[x]`.
- [ ] `dotnet build src` succeeds with 0 errors.
- [ ] `dotnet test tests` passes (existing 89 Phase 1 tests + new Phase 2 tests).
- [ ] `dotnet run --project src` launches and the sidebar is functional.
- [ ] `manual_test_phase2.sh` (to be created) completes successfully.
- [ ] `docs/phases/phase-2/TOFIX.md` has no open items.
- [ ] No regressions in Phase 1 features: new file, open file, save, dirty close, find/replace, status bar, tab switching.

---

## 13. One Recommended Path Forward

This plan proposes a **single, conservative approach**: build a clean service abstraction, add the sidebar using the existing manual-grid strategy, keep all Phase 1 code untouched, and verify every milestone before moving on. This maximizes the chance that the codebase stays in a solid, shippable state throughout Phase 2.

If you approve, the first concrete actions will be:
1. Create `docs/phases/phase-2/TOFIX.md`.
2. Implement M1 (`IFileSystemService`, `ProjectLoader`, models, tests).
3. Open a checkpoint review before touching `MainWindow.axaml` in M2.
