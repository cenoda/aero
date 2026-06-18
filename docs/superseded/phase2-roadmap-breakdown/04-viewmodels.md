# ⛔ SUPERSEDED — 4. ViewModels

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative ViewModel designs.
> Superseded: `FileTreeNodeViewModel`, `FileExplorerViewModel` (router-based open).
> PROJECT_PLAN uses: `FileExplorerNodeViewModel`, `FileExplorerViewModel` (direct `DocumentManager.OpenDocumentAsync`).

---

---

## 4.1 `FileTreeNodeViewModel`

**File:** `src/ViewModels/FileTreeNodeViewModel.cs`

### Responsibilities
- Wrap one `FileSystemNode` for binding.
- Track `IsExpanded`, `IsSelected`.
- Expose lazy-loading `Children` collection (`ObservableCollection<FileTreeNodeViewModel>` for binding).
- Provide `Kind` icon hint for the view (e.g., folder/file icon key).
- Provide `GitStatus` property (defaults to `None`) for Phase 7 forward-compatibility.
- Keep the underlying model as plain immutable data; the VM collection is the UI binding wrapper.

### Behavior
- When `IsExpanded` changes to `true` and `Children` is empty, asynchronously load children via `IFileSystemService`.
- If the node represents a project root, merge `ProjectNode` children if available.
- Double-click / Enter on a file publishes `OpenDocumentRequest`.
- If enumeration fails (permission denied, etc.), add a placeholder child node with an error icon and message so the user sees feedback, not a silently empty folder.

**No direct View references.** All interaction is through commands and messages.

---

## 4.2 `FileExplorerViewModel`

**File:** `src/ViewModels/FileExplorerViewModel.cs`

### Responsibilities
- Own the root `FileTreeNodeViewModel` list.
- Subscribe to `FolderOpened` and `FileSystemChanged` via `IMessageBus` (constructor-injected).
- React to `FolderOpened` by loading a new root.
- React to `FileSystemChanged` by refreshing the affected subtree.
- Provide context-menu commands.

### Commands
- `OpenSelectedFileCommand`
- `NewFileCommand`
- `NewFolderCommand`
- `DeleteCommand`
- `RenameCommand`
- `RefreshCommand`

### Behavior
- On `FolderOpened(Path)`, load root nodes asynchronously, apply ignore list, load projects, and restore expansion/selection from `WorkspaceService`.
- On `FileSystemChanged`, locate the affected node and reload its parent directory.
- For `New File` / `New Folder`, request a name via `PromptUserInput`; on commit call `IFileSystemService` and refresh the parent.
- For `Rename`, request the new name via `PromptUserInput`; on commit call `IFileSystemService.RenameAsync`.
- For `Delete`, request confirmation using a message-bus dialog callback, then call `DeleteAsync`.
- Surface errors through `StatusBarMessageRequested`.
- Maintain a `SelectedNode` property so context-menu commands always target the correct node.
- When `FolderOpened` is raised for a new root, stop/dispose the previous watcher before starting a new watcher.

### Acceptance
- Opening a folder shows files and directories within 100 ms for small folders.
- Ignored directories are never visible.
- External file creation appears in the tree after the debounce window.

---

## 4.3 `ShellViewModel` Changes

**File:** `src/ViewModels/ShellViewModel.cs`

### Changes
- Add `FileExplorerViewModel` property.
- Add `OpenFolderCommand` (menu + key binding `Ctrl+Shift+O`).
- Implement folder picking via Avalonia `StorageProvider.OpenFolderPickerAsync` (same app-lifetime pattern already used by `OpenFileAsync`).
- Subscribe to `OpenDocumentRequest` and call `EditorViewModel.OpenFileAsync`.
- Subscribe to `StatusBarMessageRequested` and update `StatusText` (service-originated status updates). This is mandatory — file-operation errors must be visible.
- On folder open, publish `FolderOpened` and persist through `WorkspaceService`.
- On app exit, trigger workspace save from the app exit hook (`App.axaml.cs`) before DI disposal. The concrete mechanism: in `App.axaml.cs`'s `OnDesktopExit`, resolve `IWorkspaceService` from the still-alive container, call `SaveAsync`, then dispose the container.
- Stop showing just a raw folder path in the status bar after `FolderOpened`; use a friendlier format like `"Ready — /home/user/project"`.

### Acceptance
- `File → Open Folder` opens a folder picker; on OK the tree is populated.
- Clicking a file in the tree opens a tab in the editor.
- Service-level errors (permission denied, etc.) appear in the status bar.