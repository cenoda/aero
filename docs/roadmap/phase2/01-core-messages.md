# 1. Core Messages

> **Parent:** [Phase 2 README](./README.md)
>
> **File to edit:** `src/Core/Messages.cs`

---

## New Records

| Record | Purpose |
|--------|---------|
| `OpenDocumentRequest(string FilePath)` | Published by the file tree when a file should be opened in the editor. `ShellViewModel` subscribes and routes it to `EditorViewModel.OpenFileAsync`. |
| `FileSystemChanged(string RootPath, string ChangedPath, FileSystemChangeKind Kind)` | Published by `FileSystemWatcherService` after debouncing. `FileExplorerViewModel` refreshes the affected subtree. |
| `StatusBarMessageRequested(string Text)` | Optional helper so services can surface errors without referencing the status bar directly. |
| `PromptUserInput(string Prompt, string DefaultValue, Action<string?> OnResponse)` | ViewModel-safe dialog bridge for text input (rename / new item name) using the same callback message pattern as `ConfirmDirtyClose`. |

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