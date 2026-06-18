# ⛔ SUPERSEDED — 5. Views & Layout

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative view designs.
> Superseded: `InputDialog` (single dialog), `MainWindow.axaml.cs` `PromptUserInput` subscriber.
> PROJECT_PLAN uses: `TextInputDialog` + `ConfirmDialog` (two dialogs), `PromptNewItem` / `PromptRename` / `ConfirmDelete` subscribers.

---

---

## 5.1 `FileExplorerView.axaml`

**Files:**
- `src/Views/FileExplorerView.axaml`
- `src/Views/FileExplorerView.axaml.cs`

### Requirements
- `TreeView` with hierarchical data template.
- Each node shows a `MaterialIcon` (`Folder`, `FileDocument`, `CodeBraces`, `Nodejs`, etc.) and name.
- Context menu bound to `FileExplorerViewModel` commands.
- Keyboard accessibility: Arrow keys navigate tree, Enter opens selected file, Delete triggers delete command, F2 triggers rename. Menu key or Shift+F10 opens context menu.
- Minimal code-behind; logic lives in the ViewModel.

---

## 5.2 `InputDialog.axaml`

**Files:**
- `src/Views/InputDialog.axaml`
- `src/Views/InputDialog.axaml.cs`

### Requirements
- Simple dialog with a prompt label, text box, OK, and Cancel.
- Opened from `MainWindow.axaml.cs` in response to `PromptUserInput` messages (mirrors the existing `ConfirmDirtyClose` → `DirtyCloseDialog` pattern). ViewModels never reference this dialog directly.
- Provide `static Task<string?> ShowAsync(Window owner, string prompt, string defaultValue)`.
- Avoids adding a dependency like `DialogHost.Avalonia` before Phase 8.

### Code-behind skeleton
```csharp
public partial class InputDialog : Window
{
    public InputDialog()
    {
        InitializeComponent();
    }

    public static Task<string?> ShowAsync(Window owner, string prompt, string defaultValue)
    {
        var dialog = new InputDialog();
        dialog.PromptText.Text = prompt;
        dialog.InputBox.Text = defaultValue ?? "";
        return dialog.ShowDialog<string?>(owner);
    }
}
```

---

## 5.3 `MainWindow.axaml` Changes

**File:** `src/MainWindow.axaml`

### Changes
- Wrap the existing editor area in a two-column `Grid`:
  - Column 0: `FileExplorerView` with `Width="280"`, `IsVisible="{Binding IsFileExplorerVisible}"`.
  - Column 1: existing editor `Grid`.
- Add a `GridSplitter` (Width="4") between the two columns so users can resize sidebar width.
- Add `Open Folder` to the `File` menu and key binding `Ctrl+Shift+O`.
- Keep the status bar as-is.
- NOTE: This fixed Grid layout is temporary scaffolding. Phase 8 will replace it with `Dock.Avalonia` dockable panels.

---

## 5.4 `MainWindow.axaml.cs` Changes

**File:** `src/MainWindow.axaml.cs`

### Changes
- Add `_promptUserInputHandler` field alongside the existing `_confirmDirtyCloseHandler`.
- Subscribe to `PromptUserInput` in `Initialize()`:
  ```csharp
  _promptUserInputHandler = msg =>
  {
      _ = HandlePromptUserInputAsync(msg); // fire-and-forget; dialog is modal anyway
  };
  _bus.Subscribe(_promptUserInputHandler);
  ```
- `HandlePromptUserInputAsync` calls `InputDialog.ShowAsync(this, msg.Prompt, msg.DefaultValue)` and invokes `msg.OnResponse(result)`.
- Unsubscribe in the existing `OnClosing` / cleanup path alongside `_confirmDirtyCloseHandler`.
- Extend `UnsubscribeBus()` to also unsubscribe `PromptUserInput`.

```csharp
private async Task HandlePromptUserInputAsync(PromptUserInput msg)
{
    var result = await InputDialog.ShowAsync(this, msg.Prompt, msg.DefaultValue);
    msg.OnResponse(result);
}
```