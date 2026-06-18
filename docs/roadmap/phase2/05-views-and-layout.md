# 5. Views & Layout

> **Parent:** [Phase 2 README](./README.md)

---

## 5.1 `FileExplorerView.axaml`

**Files:**
- `src/Views/FileExplorerView.axaml`
- `src/Views/FileExplorerView.axaml.cs`

### Requirements
- `TreeView` with hierarchical data template.
- Each node shows a `MaterialIcon` (`Folder`, `FileDocument`, `CodeBraces`, `Nodejs`, etc.) and name.
- Editable header (TextBox) shown when `IsEditingName` is true.
- Context menu bound to `FileExplorerViewModel` commands.
- Minimal code-behind; logic lives in the ViewModel.

---

## 5.2 `InputDialog.axaml`

**Files:**
- `src/Views/InputDialog.axaml`
- `src/Views/InputDialog.axaml.cs`

### Requirements
- Simple dialog with a prompt label, text box, OK, and Cancel.
- Opened from the window layer in response to `PromptUserInput` messages (ViewModels never reference this dialog directly).
- Avoids adding a dependency like `DialogHost.Avalonia` before Phase 8.

---

## 5.3 `MainWindow.axaml` Changes

**File:** `src/MainWindow.axaml`

### Changes
- Wrap the existing editor area in a two-column `Grid`:
  - Column 0: `FileExplorerView` with `Width="280"`, `IsVisible="{Binding IsFileExplorerVisible}"`.
  - Column 1: existing editor `Grid`.
- Add a `GridSplitter` between the two columns so users can resize sidebar width.
- Add `Open Folder` to the `File` menu and key binding `Ctrl+Shift+O`.
- Keep the status bar as-is.