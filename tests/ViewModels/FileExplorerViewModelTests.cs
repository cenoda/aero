using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aero.Core;
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
    private readonly StubProjectLoader _projects;
    private readonly FileExplorerViewModel _vm;

    public FileExplorerViewModelTests()
    {
        _fs = new MockFileSystemService(new IgnoreList(new string[0]));
        _projects = new StubProjectLoader();
        _vm = new FileExplorerViewModel(_fs, _projects, _bus);
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

    // --- nested children ----------------------------------------------

    [Fact]
    public async Task LoadFolderAsync_RecursesIntoSubdirectories()
    {
        var root = TempPath();
        Seed(
            Path.Combine(root, "src/"),
            Path.Combine(root, "src", "app.cs"),
            Path.Combine(root, "src", "Program.cs"));

        await _vm.LoadFolderAsync(root);

        var srcNode = Assert.Single(_vm.RootNodes);
        Assert.True(srcNode.IsDirectory);
        Assert.Equal(2, srcNode.Children.Count);
        Assert.All(srcNode.Children, c => Assert.False(c.IsDirectory));
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

        Assert.Equal("MicrosoftVisualStudio", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_CsprojGetsCsharpIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "app.csproj"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("LanguageCsharp", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_PackageJsonGetsNodeIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "package.json"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("Nodejs", _vm.RootNodes[0].IconKind);
    }

    [Fact]
    public async Task LoadFolderAsync_PlainFileGetsFileDocumentIcon()
    {
        var root = TempPath();
        Seed(Path.Combine(root, "README.md"));

        await _vm.LoadFolderAsync(root);

        Assert.Equal("FileDocument", _vm.RootNodes[0].IconKind);
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
        var vmWithIgnore = new FileExplorerViewModel(mock, _projects, _bus);
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
