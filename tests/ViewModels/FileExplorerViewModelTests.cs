using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Project;
using Aero.Services;
using Aero.Tests.Stubs;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="FileExplorerViewModel"/>. Uses an in-memory
/// <see cref="MockFileSystemService"/> so the tree-building logic can be
/// verified without touching the real disk.
/// </summary>
public class FileExplorerViewModelTests : IDisposable
{
    private readonly StubMessageBus _bus = new();
    private readonly MockFileSystemService _fs;
    private readonly MockFileSystemWatcherService _watcher;
    private readonly StubProjectLoader _projects;
    private readonly IDocumentManagementService _documentManager;
    private readonly FileExplorerViewModel _vm;

    public FileExplorerViewModelTests()
    {
        _fs = new MockFileSystemService(new IgnoreList(new string[0]));
        _watcher = new MockFileSystemWatcherService(_bus);
        _projects = new StubProjectLoader();
        _documentManager = new DocumentManager(_bus, new LanguageDetectionService());
        _vm = new FileExplorerViewModel(_fs, _projects, _documentManager, _watcher, _bus);
    }

    public void Dispose() => _vm.Dispose();

    // --- helpers -------------------------------------------------------

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "aero-fevm-" + Guid.NewGuid().ToString("N"));

    private void Seed(params string[] paths)
    {
        foreach (var p in paths)
        {
            if (p.EndsWith("/"))
                _fs.AddDirectory(p.TrimEnd('/'));
            else if (Directory.Exists(p))
                _fs.AddDirectory(p);
            else
            {
                _fs.AddFile(p);
                // Mirror to the project loader so icon tests can detect it.
                var kind = DetectKindFromName(p);
                if (kind.HasValue)
                    _projects.Add(new ProjectInfo(Path.GetFullPath(p), Path.GetFileName(p), kind.Value));
            }
        }
    }

    private static Aero.Models.Project.ProjectKind? DetectKindFromName(string p)
    {
        var name = Path.GetFileName(p);
        if (string.Equals(name, "package.json", StringComparison.Ordinal))
            return Aero.Models.Project.ProjectKind.NodeProject;
        return Path.GetExtension(p).ToLowerInvariant() switch
        {
            ".sln" => Aero.Models.Project.ProjectKind.Solution,
            ".csproj" => Aero.Models.Project.ProjectKind.CSharpProject,
            _ => null,
        };
    }

    // --- initial state -------------------------------------------------

    [Fact]
    public void Constructor_RootNodesIsEmpty()
    {
        Assert.Empty(_vm.RootNodes);
    }

    [Fact]
    public void Constructor_RootPathIsNull()
    {
        Assert.Null(_vm.RootPath);
    }

    [Fact]
    public void Constructor_NotLoading()
    {
        Assert.False(_vm.IsLoading);
    }

    // --- load a flat folder -------------------------------------------

    [Fact]
    public async Task LoadFolderAsync_FlatFolder_PopulatesRootNodes()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "README.md"),
            Path.Combine(root, "app.cs"),
            Path.Combine(root, "src/"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal(3, _vm.RootNodes.Count);
        // Directories before files (per service contract).
        Assert.True(_vm.RootNodes[0].IsDirectory);
        Assert.Equal("src", _vm.RootNodes[0].Name);
    }

    [Fact]
    public async Task LoadFolderAsync_SetsHasRootPath()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        await _vm.LoadFolderAsync(root);

        Assert.True(_vm.HasRootPath);
    }

    [Fact]
    public async Task LoadFolderAsync_SetsRootPath_Normalized()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        await _vm.LoadFolderAsync(root);

        Assert.NotNull(_vm.RootPath);
        Assert.Equal(Path.GetFullPath(root), _vm.RootPath);
    }

    [Fact]
    public async Task LoadFolderAsync_ClearsIsLoading_OnCompletion()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        await _vm.LoadFolderAsync(root);

        Assert.False(_vm.IsLoading);
    }

    [Fact]
    public async Task LoadFolderAsync_UpdatesStatusText()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt"));

        await _vm.LoadFolderAsync(root);

        // Status ends with the entry count.
        Assert.Contains("2 entries", _vm.StatusText);
    }

    // --- lazy-load (single-level root) -------------------------------

    [Fact]
    public async Task LoadFolderAsync_DirectoriesStartUnloaded()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "app.cs"));

        await _vm.LoadFolderAsync(root);

        var srcNode = Assert.Single(_vm.RootNodes);
        Assert.True(srcNode.IsDirectory);
        Assert.False(srcNode.AreChildrenLoaded);
    }

    [Fact]
    public async Task LoadFolderAsync_DirectoriesHaveOnlyPlaceholderChild()
    {
        // The placeholder exists solely so the TreeView renders an expander arrow
        // for unloaded directories. The view sees one entry, the VM knows it's
        // not real data.
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));

        await _vm.LoadFolderAsync(root);

        var srcNode = Assert.Single(_vm.RootNodes);
        var child = Assert.Single(srcNode.Children);
        Assert.True(child.IsPlaceholder);
    }

    [Fact]
    public async Task LoadFolderAsync_FilesHaveNoChildren()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        await _vm.LoadFolderAsync(root);

        var node = Assert.Single(_vm.RootNodes);
        Assert.False(node.IsDirectory);
        Assert.True(node.AreChildrenLoaded); // files are "loaded" by definition
        Assert.Empty(node.Children);
    }

    [Fact]
    public async Task LoadFolderAsync_DoesNotEnumerateSubdirectories()
    {
        // Regression for R3.2: opening a folder must NOT eagerly walk every
        // nested directory. This test seeds a deep tree and asserts only the
        // root level is touched.
        var root = TempPath();
        Seed(
            Path.Combine(root, "level1/"),
            Path.Combine(root, "level1", "level2/"),
            Path.Combine(root, "level1", "level2", "level3/"),
            Path.Combine(root, "level1", "level2", "level3", "deep.txt"));

        await _vm.LoadFolderAsync(root);

        // Exactly one direct child (level1) and no further enumeration.
        var level1 = Assert.Single(_vm.RootNodes);
        Assert.Equal("level1", level1.Name);
        // level1 has only its placeholder child — no level2 entry yet.
        Assert.Single(level1.Children);
        Assert.True(level1.Children[0].IsPlaceholder);
    }

    [Fact]
    public async Task LoadFolderAsync_NodeExposesFullPath()
    {
        var root = TempPath();
        var file = Path.Combine(root, "a.txt");
        Seed(file);

        await _vm.LoadFolderAsync(root);

        var node = Assert.Single(_vm.RootNodes);
        Assert.Equal(file, node.FullPath);
    }

    // --- project icons -------------------------------------------------

    [Fact]
    public async Task LoadFolderAsync_RecognizedProjectFile_GetsSolutionIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "app.sln"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Icon.Project", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_CsprojGetsCsharpIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "app.csproj"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Icon.Project", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_PackageJsonGetsNodeIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "package.json"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Icon.Config", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_PlainFileGetsFileDocumentIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "README.md"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Icon.Text", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_DirectoryGetsFolderIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Folder", _vm.RootNodes[0].IconKind);
    }

    // --- ignore list integration -------------------------------------

    [Fact]
    public async Task LoadFolderAsync_IgnoreListFiltersSubtreeRoots()
    {
        // The mock FS honours an IIgnoreList passed at construction. Seed both
        // wanted and unwanted paths; verify only wanted reach the VM.
        var root = TempPath();
        var mock = new MockFileSystemService(new IgnoreList(new[] { "bin", "node_modules" }));
        var watcherForIgnore = new MockFileSystemWatcherService();
        var vmWithIgnore = new FileExplorerViewModel(mock, _projects, _documentManager, watcherForIgnore, _bus);
        try
        {
            mock.AddDirectory(Path.Combine(root, "src"));
            mock.AddFile(Path.Combine(root, "src", "app.cs"));
            mock.AddDirectory(Path.Combine(root, "bin"));     // should be filtered
            mock.AddDirectory(Path.Combine(root, "node_modules")); // ditto

            await vmWithIgnore.LoadFolderAsync(root);

            var names = vmWithIgnore.RootNodes.Select(n => n.Name).ToList();
            Assert.Contains("src", names);
            Assert.DoesNotContain("bin", names);
            Assert.DoesNotContain("node_modules", names);
        }
        finally { vmWithIgnore.Dispose(); }
    }

    // --- EnsureChildrenLoadedAsync (lazy-load) -----------------------

    [Fact]
    public async Task EnsureChildrenLoadedAsync_PopulatesChildrenAndClearsPlaceholder()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "app.cs"),
            Path.Combine(root, "src", "Program.cs"));

        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        await _vm.EnsureChildrenLoadedAsync(srcNode);

        Assert.True(srcNode.AreChildrenLoaded);
        Assert.Equal(2, srcNode.Children.Count);
        Assert.DoesNotContain(srcNode.Children, c => c.IsPlaceholder);
        Assert.All(srcNode.Children, c => Assert.False(c.IsDirectory));
    }

    [Fact]
    public async Task EnsureChildrenLoadedAsync_OnFile_IsNoOp()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        await _vm.EnsureChildrenLoadedAsync(fileNode);

        Assert.Empty(fileNode.Children); // files never have children
    }

    [Fact]
    public async Task EnsureChildrenLoadedAsync_DoubleCall_OnlyLoadsOnce()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"));

        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        await _vm.EnsureChildrenLoadedAsync(srcNode);
        var firstChildren = srcNode.Children.ToList();

        // Second call: should be a no-op — children unchanged.
        await _vm.EnsureChildrenLoadedAsync(srcNode);

        Assert.Equal(firstChildren.Count, srcNode.Children.Count);
    }

    [Fact]
    public async Task EnsureChildrenLoadedAsync_RapidExpandCollapses_CancelsPrevious()
    {
        // Set up a mock that blocks (we don't have a blocking primitive, so we
        // simulate by issuing two EnsureChildrenLoadedAsync calls back-to-back
        // and asserting the first one's children are replaced by the second's
        // rather than duplicated).
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"));

        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        // Fire two expansions concurrently. The second should win; the first's
        // result must NOT land in node.Children (would cause duplicates).
        var first = _vm.EnsureChildrenLoadedAsync(srcNode);
        var second = _vm.EnsureChildrenLoadedAsync(srcNode);

        await Task.WhenAll(first, second);

        // Exactly one child (the seeded a.txt), no placeholder, no duplicates.
        var child = Assert.Single(srcNode.Children);
        Assert.False(child.IsPlaceholder);
        Assert.Equal("a.txt", child.Name);
        Assert.True(srcNode.AreChildrenLoaded);
    }

    [Fact]
    public async Task EnsureChildrenLoadedAsync_LoadFolderCancelsInflightChildLoads()
    {
        // Starting a new root load must cancel any in-flight child expansions
        // so the new tree starts clean.
        var root = TempPath();
        var newRoot = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"));

        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        Assert.Equal("src", srcNode.Name);

        // Start a child expansion, then immediately load a different folder.
        var childLoadTask = _vm.EnsureChildrenLoadedAsync(srcNode);
        Seed(Path.Combine(newRoot, "x.txt"));
        await _vm.LoadFolderAsync(newRoot);

        // Awaiting the cancelled child task is safe (swallows OperationCanceledException).
        await childLoadTask;

        // The new tree is just "x.txt".
        var newNode = Assert.Single(_vm.RootNodes);
        Assert.Equal("x.txt", newNode.Name);
    }

    [Fact]
    public async Task EnsureChildrenLoadedAsync_NullNode_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _vm.EnsureChildrenLoadedAsync(null!));
    }

    // --- cancellation & switching folders ----------------------------

    [Fact]
    public async Task LoadFolderAsync_SwitchingFoldersCancelsPrevious()
    {
        var root1 = TempPath();
        var root2 = TempPath();
        Seed(
            Path.Combine(root1, "a.txt"),
            Path.Combine(root2, "b.txt"));

        // Start two loads back-to-back. The second must win.
        var first = _vm.LoadFolderAsync(root1);
        var second = _vm.LoadFolderAsync(root2);

        await Task.WhenAll(first, second);

        // Final state reflects the second folder.
        Assert.Equal(Path.GetFullPath(root2), _vm.RootPath);
        var node = Assert.Single(_vm.RootNodes);
        Assert.Equal("b.txt", node.Name);
    }

    [Fact]
    public async Task LoadFolderAsync_CancelledLoad_DoesNotSetErrorMessage()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));

        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        await _vm.LoadFolderAsync(root, cts.Token);

        Assert.Null(_vm.ErrorMessage);
        Assert.False(_vm.IsLoading);
    }

    [Fact]
    public async Task LoadFolderAsync_EmptyPath_IsNoOp()
    {
        await _vm.LoadFolderAsync("");
        Assert.Null(_vm.RootPath);
        Assert.Empty(_vm.RootNodes);
    }

    // --- error handling -----------------------------------------------

    [Fact]
    public async Task LoadFolderAsync_NonexistentPath_SetsErrorMessage()
    {
        var bogus = Path.Combine(Path.GetTempPath(), "aero-definitely-missing-" + Guid.NewGuid().ToString("N"));

        await _vm.LoadFolderAsync(bogus);

        Assert.NotNull(_vm.ErrorMessage);
        Assert.Null(_vm.RootPath);
        Assert.False(_vm.HasRootPath);
        Assert.Empty(_vm.RootNodes);
    }

    // --- refresh ------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_NoRootPath_IsNoOp()
    {
        await _vm.RefreshAsync();
        Assert.Null(_vm.RootPath);
    }

    [Fact]
    public async Task RefreshAsync_ReEnumeratesCurrentRoot()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);

        // Add a new file directly to the mock; refresh should pick it up.
        _fs.AddFile(Path.Combine(root, "b.txt"));

        await _vm.RefreshAsync();

        Assert.Equal(2, _vm.RootNodes.Count);
    }

    // --- MessageBus integration --------------------------------------

    [Fact]
    public async Task FolderOpenedMessage_TriggersLoad()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "x.txt"));

        _bus.Publish(new FolderOpened(root));

        // Give the fire-and-forget handler time to complete.
        await WaitFor(() => _vm.RootPath != null, TimeSpan.FromSeconds(2));

        Assert.Equal(Path.GetFullPath(root), _vm.RootPath);
    }

    [Fact]
    public void Dispose_UnsubscribesFromMessageBus()
    {
        // After Dispose, publishing FolderOpened must not trigger a load.
        var root = TempPath();
        Seed(Path.Combine(root, "x.txt"));

        _vm.Dispose();

        // If the subscription were still active, this would throw because the
        // mock FS would try to enumerate an empty path and fail — but more
        // importantly, no exception should escape and RootPath stays null.
        _bus.Publish(new FolderOpened(root));
        Assert.Null(_vm.RootPath);
    }

    // --- FileSystemWatcher integration -------------------------------

    [Fact]
    public async Task FolderOpenedMessage_StartsWatchingPath()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "x.txt"));

        _bus.Publish(new FolderOpened(root));

        await WaitFor(() => _vm.RootPath != null, TimeSpan.FromSeconds(2));

        Assert.Single(_watcher.WatchedPaths);
        Assert.Equal(Path.GetFullPath(root), _watcher.CurrentPath);
    }

    [Fact]
    public async Task FolderChangedMessage_RefreshesRootPath()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        Assert.Single(_vm.RootNodes);

        // Add a new file to the mock file system and simulate the watcher
        // noticing it. The VM should reload and pick up the new file.
        _fs.AddFile(Path.Combine(root, "b.txt"));
        _watcher.RaiseFolderChanged(root);

        await WaitFor(() => _vm.RootNodes.Count == 2, TimeSpan.FromSeconds(2));

        Assert.Contains(_vm.RootNodes, n => n.Name == "b.txt");
    }

    [Fact]
    public void Dispose_StopsWatching()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "x.txt"));
        _bus.Publish(new FolderOpened(root));

        _vm.Dispose();

        Assert.False(_watcher.IsWatching);
        Assert.NotNull(_watcher.StoppedPaths);
    }

    // --- file activation ---------------------------------------------

    [Fact]
    public async Task OpenSelectedFileAsync_NormalizesDotDotPath()
    {
        // R1.5: FileExplorerViewModel must normalize paths before handing them
        // to DocumentManager so the same file opened via different relative
        // forms does not create duplicate tabs.
        var root = TempPath();
        Directory.CreateDirectory(root);
        var realFile = Path.Combine(root, "a.txt");
        await File.WriteAllTextAsync(realFile, "hello");

        var nonNormalized = Path.Combine(root, "..", Path.GetFileName(root), "a.txt");

        _vm.SelectedNode = new FileExplorerNodeViewModel("a.txt", nonNormalized, false, "FileDocument");
        await _vm.OpenSelectedFileAsync();

        var doc = Assert.Single(_documentManager.Documents);
        Assert.Equal(Path.GetFullPath(realFile), doc.FilePath);
    }

    [Fact]
    public async Task OpenSelectedFileAsync_Directory_DoesNothing()
    {
        _vm.SelectedNode = new FileExplorerNodeViewModel("src", "/tmp/src", true, "Folder");
        await _vm.OpenSelectedFileAsync();
        Assert.Empty(_documentManager.Documents);
    }

    [Fact]
    public async Task OpenSelectedFileAsync_NullSelectedNode_DoesNothing()
    {
        _vm.SelectedNode = null;
        await _vm.OpenSelectedFileAsync();
        Assert.Empty(_documentManager.Documents);
    }

    [Fact]
    public async Task OpenFileAsync_NormalizesBeforeOpening()
    {
        var root = TempPath();
        Directory.CreateDirectory(root);
        var realFile = Path.Combine(root, "a.txt");
        await File.WriteAllTextAsync(realFile, "hello");

        var nonNormalized = Path.Combine(root, "..", Path.GetFileName(root), "a.txt");
        var node = new FileExplorerNodeViewModel("a.txt", nonNormalized, false, "FileDocument");

        await _vm.OpenFileAsync(node);

        var doc = Assert.Single(_documentManager.Documents);
        Assert.Equal(Path.GetFullPath(realFile), doc.FilePath);
    }

    // --- NewFileCommand ------------------------------------------------

    [Fact]
    public async Task NewFileCommand_SelectedDirectory_CreatesFileInside()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        // Expand so children collection exists (though empty aside from placeholder).
        await _vm.EnsureChildrenLoadedAsync(srcNode);

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult("newfile.cs"));

        await _vm.NewFileCommand.Execute(srcNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "src", "newfile.cs")));
    }

    [Fact]
    public async Task NewFileCommand_SelectedFile_CreatesInParentDirectory()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        await _vm.EnsureChildrenLoadedAsync(srcNode);
        var fileNode = Assert.Single(srcNode.Children);

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult("newfile.cs"));

        await _vm.NewFileCommand.Execute(fileNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "src", "newfile.cs")));
    }

    [Fact]
    public async Task NewFileCommand_Cancelled_DoesNotCreate()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult(null));

        await _vm.NewFileCommand.Execute(srcNode).FirstAsync();

        // Only the seeded directory exists.
        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src", "newfile.cs")));
    }

    [Fact]
    public async Task NewFileCommand_NoSelection_NoOp()
    {
        // When node is null, the handler should return early.
        await _vm.NewFileCommand.Execute(null).FirstAsync();

        Assert.Empty(_vm.RootNodes);
    }

    [Fact]
    public async Task NewFileCommand_DoesNotAffectDocumentManager()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        var docsBefore = _documentManager.Documents.Count;
        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult("newfile.cs"));

        await _vm.NewFileCommand.Execute(srcNode).FirstAsync();

        Assert.Equal(docsBefore, _documentManager.Documents.Count);
    }

    // --- NewFolderCommand ---------------------------------------------

    [Fact]
    public async Task NewFolderCommand_SelectedDirectory_CreatesSubdirectory()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult("lib"));

        await _vm.NewFolderCommand.Execute(srcNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "src", "lib")));
    }

    [Fact]
    public async Task NewFolderCommand_Cancelled_DoesNotCreate()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult(null));

        await _vm.NewFolderCommand.Execute(srcNode).FirstAsync();

        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src", "lib")));
    }

    [Fact]
    public async Task NewFolderCommand_NoSelection_NoOp()
    {
        await _vm.NewFolderCommand.Execute(null).FirstAsync();
        Assert.Empty(_vm.RootNodes);
    }

    // --- RenameCommand ------------------------------------------------

    [Fact]
    public async Task RenameCommand_RenamesFile()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptRename>(msg => msg.OnResult("b.txt"));

        await _vm.RenameCommand.Execute(fileNode).FirstAsync();

        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "a.txt")));
        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "b.txt")));
    }

    [Fact]
    public async Task RenameCommand_RenamesDirectory()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "src/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptRename>(msg => msg.OnResult("lib"));

        await _vm.RenameCommand.Execute(srcNode).FirstAsync();

        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src")));
        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "lib")));
    }

    [Fact]
    public async Task RenameCommand_SameName_IsNoOp()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptRename>(msg => msg.OnResult("a.txt"));

        await _vm.RenameCommand.Execute(fileNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task RenameCommand_Cancelled_IsNoOp()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<PromptRename>(msg => msg.OnResult(null));

        await _vm.RenameCommand.Execute(fileNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task RenameCommand_NullNode_NoOp()
    {
        await _vm.RenameCommand.Execute(null).FirstAsync();
        Assert.Empty(_vm.RootNodes);
    }

    [Fact]
    public async Task RenameCommand_DoesNotAffectDocumentManager()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        var docsBefore = _documentManager.Documents.Count;
        _bus.Subscribe<PromptRename>(msg => msg.OnResult("b.txt"));

        await _vm.RenameCommand.Execute(fileNode).FirstAsync();

        Assert.Equal(docsBefore, _documentManager.Documents.Count);
    }

    // --- DeleteCommand ------------------------------------------------

    [Fact]
    public async Task DeleteCommand_Confirmed_DeletesFile()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<ConfirmDelete>(msg => msg.OnResult(true));

        await _vm.DeleteCommand.Execute(fileNode).FirstAsync();

        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task DeleteCommand_Confirmed_DeletesDirectoryRecursively()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"),
            Path.Combine(root, "src", "lib/"),
            Path.Combine(root, "src", "lib", "b.txt"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<ConfirmDelete>(msg => msg.OnResult(true));

        await _vm.DeleteCommand.Execute(srcNode).FirstAsync();

        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src")));
        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src", "a.txt")));
        Assert.False(await _fs.ExistsAsync(Path.Combine(root, "src", "lib", "b.txt")));
    }

    [Fact]
    public async Task DeleteCommand_Cancelled_DoesNotDelete()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        _bus.Subscribe<ConfirmDelete>(msg => msg.OnResult(false));

        await _vm.DeleteCommand.Execute(fileNode).FirstAsync();

        Assert.True(await _fs.ExistsAsync(Path.Combine(root, "a.txt")));
    }

    [Fact]
    public async Task DeleteCommand_NullNode_NoOp()
    {
        await _vm.DeleteCommand.Execute(null).FirstAsync();
        Assert.Empty(_vm.RootNodes);
    }

    [Fact]
    public async Task DeleteCommand_DoesNotAffectDocumentManager()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "a.txt"));
        await _vm.LoadFolderAsync(root);
        var fileNode = Assert.Single(_vm.RootNodes);

        var docsBefore = _documentManager.Documents.Count;
        _bus.Subscribe<ConfirmDelete>(msg => msg.OnResult(true));

        await _vm.DeleteCommand.Execute(fileNode).FirstAsync();

        Assert.Equal(docsBefore, _documentManager.Documents.Count);
    }

    // --- nested tree-state tests (R7.2) ------------------------------

    [Fact]
    public async Task CreateFile_Nested_UpdatesParentChildren()
    {
        // Build a 2-level tree and expand both levels, then create a file
        // inside the subdirectory and assert the Children collection updates.
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "lib/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        // Expand src so it's loaded when the create completes.
        await _vm.EnsureChildrenLoadedAsync(srcNode);
        Assert.Single(srcNode.Children); // just lib/

        _bus.Subscribe<PromptNewItem>(msg => msg.OnResult("newfile.cs"));

        await _vm.NewFileCommand.Execute(srcNode).FirstAsync();

        // src.Children should now contain newfile.cs alongside lib/.
        Assert.Equal(2, srcNode.Children.Count);
        Assert.Contains(srcNode.Children, n => n.Name == "newfile.cs");
        Assert.Contains(srcNode.Children, n => n.Name == "lib");
    }

    [Fact]
    public async Task DeleteFile_Nested_RemovesFromParentChildren()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"),
            Path.Combine(root, "src", "lib/"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        await _vm.EnsureChildrenLoadedAsync(srcNode);
        var fileNode = Assert.Single(srcNode.Children, n => n.Name == "a.txt");

        _bus.Subscribe<ConfirmDelete>(msg => msg.OnResult(true));

        await _vm.DeleteCommand.Execute(fileNode).FirstAsync();

        // src.Children should have lost a.txt, kept lib/.
        Assert.DoesNotContain(srcNode.Children, n => n.Name == "a.txt");
        Assert.Contains(srcNode.Children, n => n.Name == "lib");
    }

    [Fact]
    public async Task RenameFile_Nested_UpdatesParentChildren()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "a.txt"));
        await _vm.LoadFolderAsync(root);
        var srcNode = Assert.Single(_vm.RootNodes);
        await _vm.EnsureChildrenLoadedAsync(srcNode);
        var fileNode = Assert.Single(srcNode.Children);

        _bus.Subscribe<PromptRename>(msg => msg.OnResult("b.txt"));

        await _vm.RenameCommand.Execute(fileNode).FirstAsync();

        // src.Children should have b.txt instead of a.txt.
        Assert.DoesNotContain(srcNode.Children, n => n.Name == "a.txt");
        Assert.Contains(srcNode.Children, n => n.Name == "b.txt");
    }

    // --- small async helper ------------------------------------------

    private static async Task WaitFor(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }
    }
}
