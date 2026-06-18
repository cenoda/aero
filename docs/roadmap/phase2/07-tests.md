# 7. Tests

> **Parent:** [Phase 2 README](./README.md)

---

## 7.1 Test Project Setup

- Framework: xUnit.
- Follow the existing test-project pattern in `tests/aero.Tests.csproj`: include selected source files from `src/` with `<Compile Include="..." />` rather than adding a project reference to `src/aero.csproj`.
- Keep lightweight in-process stubs/fakes (for example `StubMessageBus`) for unit tests.
- If no solution exists at repo root, create `aero.sln` and add both projects.

---

## 7.2 Unit Tests

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

## 7.3 Integration Test (Manual)

- Open a real C# solution folder in Aero.
- Delete/create files externally and confirm tree refresh.
- Verify ignored folders are hidden.