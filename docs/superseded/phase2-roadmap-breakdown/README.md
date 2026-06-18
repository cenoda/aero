# ⛔ SUPERSEDED — Phase 2 Breakdown (roadmap version)

> **Status:** Superseded by [`docs/phases/phase-2/PROJECT_PLAN.md`](../../phases/phase-2/PROJECT_PLAN.md).
> See [`../phase2-roadmap-plan.md`](../phase2-roadmap-plan.md) for the reconciliation summary.
> **DO NOT implement from these files.**


| # | File | Content |
|---|------|---------|
| — | [README.md](./README.md) | Scope, architecture overview, implementation order, definition of done, risks |
| 1 | [01-core-messages.md](./01-core-messages.md) | New `Messages.cs` records and enums |
| 2 | [02-models.md](./02-models.md) | `FileSystemNode`, `ProjectNode`, and supporting enums |
| 3 | [03-services.md](./03-services.md) | `IgnoreList`, `FileSystemService`, `FileSystemWatcherService`, `ProjectLoader`, `WorkspaceService` |
| 4 | [04-viewmodels.md](./04-viewmodels.md) | `FileTreeNodeViewModel`, `FileExplorerViewModel`, `ShellViewModel` changes |
| 5 | [05-views-and-layout.md](./05-views-and-layout.md) | `FileExplorerView`, `InputDialog`, `MainWindow` layout changes, `MainWindow.axaml.cs` `PromptUserInput` subscriber |
| 6 | [06-di-registration.md](./06-di-registration.md) | Dependency injection wiring in `App.axaml.cs` |
| 7 | [07-tests.md](./07-tests.md) | Unit test plan, integration test checklist |

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
- Git status overlays on file icons (but `GitStatus` model enum is added now for forward-compat).
- Full solution build / project serialization.

### Library Note
- `Dock.Avalonia` (11.3.*) is already a NuGet dependency in `src/aero.csproj` but is NOT used in Phase 2. The fixed Grid layout here is temporary; Phase 8 will replace it with dockable panels via `Dock.Avalonia`.

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