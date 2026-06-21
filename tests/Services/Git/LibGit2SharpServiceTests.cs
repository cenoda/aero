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
            Arguments = string.Join(" ", args),
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
        if (!proc.WaitForExit(10_000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(" ", args)} timed out");
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
        Assert.Equal(GitFileStatusKind.Added, readme!.StagingStatus);
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
    public void Constructor_InvalidPath_ThrowsGitServiceUnavailableException()
    {
        // Arrange & Act
        var ex = Assert.Throws<GitServiceUnavailableException>(() => CreateService());

        // Assert
        Assert.NotNull(ex.InnerException);
    }
}