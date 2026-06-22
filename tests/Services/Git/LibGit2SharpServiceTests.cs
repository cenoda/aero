using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Git;
using Aero.Services.Git;
using Xunit;

namespace Aero.Tests.Services.Git;

/// <summary>
/// Integration tests for LibGit2SharpService using real temp repositories.
/// </summary>
public class LibGit2SharpServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gitDir;

    public LibGit2SharpServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aero-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _gitDir = Path.Combine(_tempDir, ".git");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* best effort */ }
    }

    private void RunGit(params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
            WorkingDirectory = _tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,   // prevent blocking on stdin
            UseShellExecute = false
        };
        // Prevent git from prompting for credentials or GPG passphrase
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_ASKPASS"] = "echo";
        psi.Environment["GPG_TTY"] = "";
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        using var proc = System.Diagnostics.Process.Start(psi)!;
        // Read stdout/stderr to prevent pipe buffer deadlocks (known .NET issue)
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(10_000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(" ", args)} timed out");
        }
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(" ", args)} failed (exit {proc.ExitCode}): {stderr}");
        }
    }

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return path;
    }

    private LibGit2SharpService CreateService()
    {
        return new LibGit2SharpService(_gitDir, _tempDir);
    }

    [Fact]
    public async Task GetRepositoryInfoAsync_WithValidRepo_ReturnsInfo()
    {
        // Arrange: create a real git repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act
        using var sut = CreateService();
        var info = await sut.GetRepositoryInfoAsync(CancellationToken.None);

        // Assert
        Assert.Equal(_tempDir, info.RootPath);
        Assert.Equal("master", info.CurrentBranch);
    }

    [Fact]
    public async Task GetStatusAsync_UntrackedFile_ReturnsUntracked()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act: add untracked file
        CreateFile("new.txt", "new content");
        using var sut = CreateService();
        var status = await sut.GetStatusAsync(CancellationToken.None);

        // Assert
        var newFile = status.FirstOrDefault(f => f.FilePath == "new.txt");
        Assert.NotNull(newFile);
        Assert.Equal(GitFileStatusKind.Untracked, newFile!.Status);
    }

    [Fact]
    public async Task GetStatusAsync_StagedFile_ReturnsAdded()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act: modify and stage file
        CreateFile("README.md", "# Updated");
        RunGit("add", "README.md");

        using var sut = CreateService();
        var status = await sut.GetStatusAsync(CancellationToken.None);

        // Assert
        var readme = status.FirstOrDefault(f => f.FilePath == "README.md");
        Assert.NotNull(readme);
        Assert.Equal(GitFileStatusKind.Modified, readme!.StagingStatus);
    }

    [Fact]
    public async Task GetBranchesAsync_AfterCommit_ReturnsCurrentBranch()
    {
        // Arrange: create a real git repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act
        using var sut = CreateService();
        var branches = await sut.GetBranchesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(branches);
        Assert.NotEmpty(branches);
        var currentBranch = branches.FirstOrDefault(b => b.IsCurrent);
        Assert.NotNull(currentBranch);
        Assert.Equal("master", currentBranch.Name);
    }

    [Fact]
    public async Task GetFileDiffAsync_StagedFile_ReturnsDiff()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test\nLine 2");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act: modify and stage
        CreateFile("README.md", "# Updated\nLine 2");
        RunGit("add", "README.md");

        using var sut = CreateService();
        var diff = await sut.GetFileDiffAsync("README.md", CancellationToken.None);

        // Assert
        Assert.NotNull(diff);
        Assert.NotEmpty(diff.Hunks);
    }

    [Fact]
    public async Task GetFileDiffAsync_UnstagedFile_ReturnsDiff()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test\nLine 2");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act: modify but don't stage
        CreateFile("README.md", "# Updated\nLine 2");

        using var sut = CreateService();
        var diff = await sut.GetFileDiffAsync("README.md", CancellationToken.None);

        // Assert
        Assert.NotNull(diff);
        Assert.NotEmpty(diff.Hunks);
    }

    [Fact]
    public async Task GetFileDiffAsync_HunkMetadata_ParsedCorrectly()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("test.cs", "line1\nline2\nline3\nline4\nline5");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        // Act: modify multiple lines
        CreateFile("test.cs", "line1\nmodified\nline3\nline4\nline5");
        
        using var sut = CreateService();
        var diff = await sut.GetFileDiffAsync("test.cs", CancellationToken.None);

        // Assert: hunk metadata should not be zeros
        Assert.NotNull(diff);
        Assert.NotEmpty(diff.Hunks);
        var hunk = diff.Hunks[0];
        Assert.NotEqual(0, hunk.OldStart);
        Assert.NotEqual(0, hunk.NewStart);
    }

    [Fact]
    public void Constructor_InvalidPath_ThrowsGitServiceUnavailableException()
    {
        // Arrange & Act
        var ex = Assert.Throws<GitServiceUnavailableException>(() => CreateService());

        // Assert
        Assert.NotNull(ex.InnerException);
    }

    // --- GetGraphAsync tests (M7-G1) ---

    [Fact]
    public async Task GetGraphAsync_LinearHistory_ReturnsCorrectCount()
    {
        // Arrange: create a repo with 5 commits
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("f1.txt", "a");
        RunGit("add", ".");
        RunGit("commit", "-m", "C1");
        CreateFile("f2.txt", "b");
        RunGit("add", ".");
        RunGit("commit", "-m", "C2");
        CreateFile("f3.txt", "c");
        RunGit("add", ".");
        RunGit("commit", "-m", "C3");
        CreateFile("f4.txt", "d");
        RunGit("add", ".");
        RunGit("commit", "-m", "C4");
        CreateFile("f5.txt", "e");
        RunGit("add", ".");
        RunGit("commit", "-m", "C5");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(10, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.Equal(5, graph.Count);
    }

    [Fact]
    public async Task GetGraphAsync_MergeCommit_IncludesBothParents()
    {
        // Arrange: create a repo with a merge commit
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("main.txt", "main");
        RunGit("add", ".");
        RunGit("commit", "-m", "C1 (main)");
        RunGit("branch", "feature");
        CreateFile("feature.txt", "feature work");
        RunGit("add", ".");
        RunGit("commit", "-m", "C2 (main)");
        RunGit("checkout", "feature");
        CreateFile("on-feature.txt", "feat");
        RunGit("add", ".");
        RunGit("commit", "-m", "C3 (feature)");
        RunGit("checkout", "master");
        RunGit("merge", "feature", "--no-edit", "-m", "Merge feature into master");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(10, CancellationToken.None);

        // The merge commit (HEAD) should have two parents
        var mergeCommit = graph.FirstOrDefault();
        Assert.NotNull(mergeCommit);
        Assert.Equal(2, mergeCommit!.ParentShas.Count);
    }

    [Fact]
    public async Task GetGraphAsync_RespectsCountLimit()
    {
        // Arrange: create a repo with 10 commits
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        for (int i = 0; i < 10; i++)
        {
            CreateFile($"f{i}.txt", $"content{i}");
            RunGit("add", ".");
            RunGit("commit", "-m", $"C{i + 1}");
        }

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(3, CancellationToken.None);

        // Should return at most 3 commits
        Assert.NotNull(graph);
        Assert.Equal(3, graph.Count);
    }

    [Fact]
    public async Task GetGraphAsync_CommitHasCorrectMetadata()
    {
        // Arrange: create a repo with one commit
        RunGit("init");
        RunGit("config", "user.email", "author@test.com");
        RunGit("config", "user.name", "Test Author");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(5, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
        var commit = graph[0];
        Assert.False(string.IsNullOrEmpty(commit.Sha));
        Assert.Equal("Initial commit", commit.Message);
        Assert.Equal("Test Author", commit.Author);
        Assert.NotEqual(default, commit.AuthorDate);
    }

    [Fact]
    public async Task GetGraphAsync_InitialCommit_HasNoParents()
    {
        // Arrange: create a repo with one commit (no parents)
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(5, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
        var initialCommit = graph.Last(); // last in the list = oldest
        Assert.Empty(initialCommit.ParentShas);
    }

    [Fact]
    public async Task GetGraphAsync_CurrentBranch_IsLabeled()
    {
        // Arrange: create a repo on 'main' (or 'master') branch
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(5, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
        var headCommit = graph[0];
        // HEAD commit should have at least one branch label (the current branch)
        Assert.NotEmpty(headCommit.BranchLabels);
    }

    // --- Packed-refs test (G3) ---

    [Fact]
    public async Task GetGraphAsync_PackedRefs_IncludesBranchLabels()
    {
        // Arrange: create a repo and force branches into packed-refs
        RunGit("init");
        RunGit("config", "user.email", "test@test.com");
        RunGit("config", "user.name", "test");
        CreateFile("README.md", "# Test");
        RunGit("add", ".");
        RunGit("commit", "-m", "Initial commit");
        RunGit("branch", "feature1");
        RunGit("branch", "feature2");

        // Pack refs to move branch pointers into packed-refs
        RunGit("pack-refs", "--all");

        using var sut = CreateService();
        var graph = await sut.GetGraphAsync(5, CancellationToken.None);

        // Assert: HEAD commit should have branch labels despite being in packed-refs
        Assert.NotNull(graph);
        Assert.NotEmpty(graph);
        var headCommit = graph[0];
        // On 'master' or 'main' depending on git version
        Assert.NotEmpty(headCommit.BranchLabels);
    }
}