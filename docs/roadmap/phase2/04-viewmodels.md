# 4. ViewModels

> **Parent:** [Phase 2 README](./README.md)

---

## 4.1 `FileTreeNodeViewModel`

**File:** `src/ViewModels/FileTreeNodeViewModel.cs`

### Responsibilities
- Wrap one `FileSystemNode` for binding.
- Track `IsExpanded`, `IsSelected`, `IsEditingName`, `EditingName`.
- Expose lazy-loading `Children` collection (`ObservableCollection<FileTreeNodeViewModel>` for binding).
- Provide `Kind` icon hint for the view (e.g., folder/file icon key).
- Keep the underlying model as plain immutable data; the VM collection is the UI binding wrapper.

### Behavior
- When `IsExpanded` changes to `true` and `Children` is empty, asynchronously load children via `IFileSystemService`.
- If the node represents a project root, merge `ProjectNode` children if available.
- Double-click / Enter on a file publishes `OpenDocumentRequest`.

**No direct View references.** All interaction is through commands and messages.

---

## 4.2 `FileExplorerViewModel`

**File:** `src/ViewModels/FileExplorerViewModel.cs`

### Responsibilities
- Own the root `FileTreeNodeViewModel` list.
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
- Subscribe to `StatusBarMessageRequested` and update `StatusText` (service-originated status updates).
- On folder open, publish `FolderOpened` and persist through `WorkspaceService`.
- On app exit, trigger workspace save from the app exit hook (`App.axaml.cs`) before DI disposal.

### Acceptance
- `File → Open Folder` opens a folder picker; on OK the tree is populated.
- Clicking a file in the tree opens a tab in the editor.