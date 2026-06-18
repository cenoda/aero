# ⛔ SUPERSEDED — 7. Tests

> See [`../../../phases/phase-2/PROJECT_PLAN.md`](../../../phases/phase-2/PROJECT_PLAN.md) for the authoritative test plan.
> Superseded: NSubstitute + Microsoft.Reactive.Testing dependencies, `<Compile Include>` for roadmap-named classes.
> PROJECT_PLAN: no new NuGet packages, uses `MockFileSystemService` + `MockFileSystemWatcherService` stubs.

---

---

## 7.1 Test Project Setup

- Framework: xUnit.
- Follow the existing test-project pattern in `tests/aero.Tests.csproj`: include selected source files from `src/` with `<Compile Include="..." />` rather than adding a project reference to `src/aero.csproj`.
- Keep lightweight in-process stubs/fakes (for example `StubMessageBus`, `StubFileSystemService`) for unit tests. Stubs go in `tests/Stubs/`.
- Add NSubstitute for mocking: `<PackageReference Include="NSubstitute" Version="5.*" />`.
- Add `Microsoft.Reactive.Testing` for `TestScheduler` (needed for debounce tests): `<PackageReference Include="Microsoft.Reactive.Testing" Version="6.*" />`.
- If no solution exists at repo root, create `aero.sln` and add both projects.

## 7.2 Source Files to Add to Test Project

Add these `<Compile Include="..." />` entries in `tests/aero.Tests.csproj`:

```xml
<!-- Phase 2 models -->
<Compile Include="../src/Models/Workspace/FileSystemNode.cs" />
<Compile Include="../src/Models/Workspace/ProjectNode.cs" />
<Compile Include="../src/Models/Workspace/FileSystemNodeKind.cs" />
<Compile Include="../src/Models/Workspace/ProjectKind.cs" />
<Compile Include="../src/Models/Workspace/GitStatus.cs" />
<Compile Include="../src/Models/Workspace/WorkspaceState.cs" />

<!-- Phase 2 services -->
<Compile Include="../src/Services/IIgnoreList.cs" />
<Compile Include="../src/Services/IgnoreList.cs" />
<Compile Include="../src/Services/IFileSystemService.cs" />
<Compile Include="../src/Services/FileSystemService.cs" />
<Compile Include="../src/Services/IFileSystemWatcherService.cs" />
<Compile Include="../src/Services/FileSystemWatcherService.cs" />
<Compile Include="../src/Services/IProjectLoader.cs" />
<Compile Include="../src/Services/ProjectLoader.cs" />
<Compile Include="../src/Services/IWorkspaceService.cs" />
<Compile Include="../src/Services/WorkspaceService.cs" />

<!-- Phase 2 ViewModels -->
<Compile Include="../src/ViewModels/FileTreeNodeViewModel.cs" />
<Compile Include="../src/ViewModels/FileExplorerViewModel.cs" />
```

The existing `<Compile Include="../src/Core/Messages.cs" />` already covers the new message records and enums (they are added inline to the same file).

---

## 7.3 Stubs to Create

---

## 7.3 Stubs to Create

| Stub | Purpose |
|------|---------|
| `StubFileSystemService` | In-memory file system for testing tree enumeration, ignore filtering, and file operations without disk IO. Lives in `tests/Stubs/StubFileSystemService.cs`. |
| `StubFileSystemWatcherService` | Triggers `FileSystemChanged` events on demand for testing ViewModel refresh behavior. |
| `StubWorkspaceService` | In-memory state store for testing persistence round-trips. |

Existing stubs: `StubMessageBus` (already in `tests/Stubs/`), `SingleThread` (for synchronizing reactive tests).

---

## 7.4 Unit Tests

| Test Class | What to Cover |
|------------|---------------|
| `IgnoreListTests` | Defaults, directory matching, file matching, custom patterns. |
| `FileSystemServiceTests` | Enumeration skips ignored entries; errors are handled. |
| `FileSystemWatcherServiceTests` | Debounce batches events; ignored paths dropped; stop/dispose safe. |
| `ProjectLoaderTests` | `.sln` parsing, `.csproj` parsing, `package.json` parsing, malformed input. |
| `WorkspaceServiceTests` | Save/load round-trip, corrupt file recovery. |
| `FileTreeNodeViewModelTests` | Expansion triggers load; open request published on file double-click. |
| `FileExplorerViewModelTests` | FolderOpened loads root; FileSystemChanged refreshes subtree. |

---

## 7.5 Integration Test (Manual)

- Open a real C# solution folder in Aero.
- Delete/create files externally and confirm tree refresh.
- Verify ignored folders are hidden.