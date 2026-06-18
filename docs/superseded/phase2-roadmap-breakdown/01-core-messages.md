# ⛔ SUPERSEDED — 1. Core Messages

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative message contracts.
> Superseded records: `OpenDocumentRequest`, `FileSystemChanged`, `StatusBarMessageRequested`, `PromptUserInput`, `FileSystemChangeKind`.
> PROJECT_PLAN uses: `FolderChanged`, `PromptNewItem`, `PromptRename`, `ConfirmDelete`.

---

---

## New Records

| Record | Purpose |
|--------|---------|
| `OpenDocumentRequest(string FilePath)` | Published by the file tree when a file should be opened in the editor. `ShellViewModel` subscribes and routes it to `EditorViewModel.OpenFileAsync`. |
| `FileSystemChanged(string RootPath, string ChangedPath, string? OldPath, FileSystemChangeKind Kind)` | Published by `FileSystemWatcherService` after debouncing. `OldPath` is non-null only for `Renamed` events (the previous path). `FileExplorerViewModel` refreshes the affected subtree. |
| `StatusBarMessageRequested(string Text)` | Published by any service or ViewModel to surface errors/warnings in the status bar. `ShellViewModel` subscribes and updates `StatusText`. Mandatory — not optional. |
| `PromptUserInput(string Prompt, string DefaultValue, Action<string?> OnResponse)` | ViewModel-safe dialog bridge for text input (rename / new item name) using the same callback message pattern as `ConfirmDirtyClose`. `MainWindow.axaml.cs` subscribes and shows `InputDialog`. |

---

## New Enum

### `FileSystemChangeKind`

```csharp
public enum FileSystemChangeKind
{
    Created,
    Deleted,
    Renamed,
    Changed
}
```

---

## Acceptance Criteria

- All four records are defined in `Messages.cs`.
- `FileSystemChangeKind` enum is defined alongside them.
- No logic — records are plain data carriers.
- Dialog interactions from ViewModels use message callbacks rather than direct view references.