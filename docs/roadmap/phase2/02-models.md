# 2. Models

> **Parent:** [Phase 2 README](./README.md)
>
> **New folder:** `src/Models/Workspace/`

---

## 2.1 `FileSystemNode`

**File:** `src/Models/Workspace/FileSystemNode.cs`

```csharp
public class FileSystemNode
{
    public string FullPath { get; init; }
    public string Name { get; init; }
    public FileSystemNodeKind Kind { get; init; }
    public IReadOnlyList<FileSystemNode> Children { get; init; } = Array.Empty<FileSystemNode>();
}
```

- Plain data object; no `INotifyPropertyChanged`, no logic.
- `Children` is initially empty; populated lazily by the ViewModel.

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

## Acceptance Criteria

- All four files exist under `src/Models/Workspace/`.
- `FileSystemNode` and `ProjectNode` are immutable-ish (init-only properties).
- No logic or dependencies in any model class.