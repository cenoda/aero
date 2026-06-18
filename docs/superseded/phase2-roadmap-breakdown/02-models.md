# ⛔ SUPERSEDED — 2. Models

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative models.
> Superseded: `FileSystemNode`, `ProjectNode`, `FileSystemNodeKind`, `ProjectKind`.
> PROJECT_PLAN uses: `FileSystemEntry`, `ProjectInfo`, `FileSystemEntryKind`, `ProjectKind` (different values).

---

---

## 2.1 `FileSystemNode`

**File:** `src/Models/Workspace/FileSystemNode.cs`

```csharp
public class FileSystemNode
{
    public string FullPath { get; init; }
    public string Name { get; init; }
    public FileSystemNodeKind Kind { get; init; }
    public bool IsSymlink { get; init; }
    public IReadOnlyList<FileSystemNode> Children { get; init; } = Array.Empty<FileSystemNode>();
}
```

- Plain data object; no `INotifyPropertyChanged`, no logic.
- `Children` is initially empty; populated lazily by the ViewModel.
- `IsSymlink` is `true` when the node is a directory symlink/junction. The `FileSystemService` does not recurse into symlinked directories to prevent infinite loops.

---

## 2.2 `ProjectNode`

**File:** `src/Models/Workspace/ProjectNode.cs`

```csharp
public class ProjectNode
{
    public string FullPath { get; init; }
    public string Name { get; init; }
    public ProjectKind Kind { get; init; }
    public IReadOnlyList<ProjectNode> Children { get; init; } = Array.Empty<ProjectNode>();
    /// <summary>
    /// Full paths of source files belonging to this project.
    /// Populated by ProjectLoader for Phase 2; consumed by Phase 4 LSP to map files to projects.
    /// </summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();
}
```

- Plain data object.

---

## 2.3 `FileSystemNodeKind`

**File:** `src/Models/Workspace/FileSystemNodeKind.cs`

```csharp
public enum FileSystemNodeKind
{
    File,
    Directory
}
```

---

## 2.4 `ProjectKind`

**File:** `src/Models/Workspace/ProjectKind.cs`

```csharp
public enum ProjectKind
{
    Solution,
    CSharpProject,
    NodePackage,
    Unknown
}
```

---

## 2.5 `GitStatus` (forward-compatible stub)

**File:** `src/Models/Workspace/GitStatus.cs`

```csharp
public enum GitStatus
{
    None,
    Modified,
    Staged,
    Added,
    Deleted,
    Untracked,
    Conflict
}
```

- Phase 2 always defaults to `GitStatus.None`.
- Phase 7 (Git Integration) will populate this from `LibGit2Sharp` status.
- Having the enum now avoids a refactor later when `FileTreeNodeViewModel` needs to expose it.

---

## Acceptance Criteria

- All four files exist under `src/Models/Workspace/`.
- `FileSystemNode` and `ProjectNode` are immutable-ish (init-only properties).
- No logic or dependencies in any model class.