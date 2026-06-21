using System;
using System.IO;
using System.Threading.Tasks;
using Aero.Services.Git;
using Xunit;

namespace Aero.Tests.Services.Git;

/// <summary>
/// Unit tests for GitServiceFactory.
/// </summary>
public class GitServiceFactoryTests : IDisposable
{
    private readonly string _testRepoPath;
    private readonly string _gitDir;

    public GitServiceFactoryTests()
    {
        // Create a temporary directory for testing
        _testRepoPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testRepoPath);
        _gitDir = Path.Combine(_testRepoPath, ".git");
    }

    /// <summary>
    /// Detect returns null for a non-git directory.
    /// </summary>
    [Fact]
    public void Detect_NonGitDirectory_ReturnsNull()
    {
        var factory = new GitServiceFactory();
        var result = factory.Detect(_testRepoPath);
        Assert.Null(result);
    }

    /// <summary>
    /// Detect returns null for an empty path.
    /// </summary>
    [Fact]
    public void Detect_EmptyPath_ReturnsNull()
    {
        var factory = new GitServiceFactory();
        var result = factory.Detect(string.Empty);
        Assert.Null(result);
    }

    /// <summary>
    /// Detect returns null for null path.
    /// </summary>
    [Fact]
    public void Detect_NullPath_ReturnsNull()
    {
        var factory = new GitServiceFactory();
        var result = factory.Detect(null!);
        Assert.Null(result);
    }

    /// <summary>
    /// Detect returns null for a fake .git directory (not a real repo).
    /// </summary>
    [Fact]
    public void Detect_FakeGitDirectory_ReturnsNull()
    {
        // Create a fake .git directory (not a real git repo)
        Directory.CreateDirectory(_gitDir);

        var factory = new GitServiceFactory();
        var result = factory.Detect(_testRepoPath);

        // LibGit2Sharp rejects non-repos, so factory returns null
        Assert.Null(result);
    }

    /// <summary>
    /// Detect returns null for non-existent path.
    /// </summary>
    [Fact]
    public void Detect_NonExistentPath_ReturnsNull()
    {
        var factory = new GitServiceFactory();
        var result = factory.Detect("/non/existent/path");
        Assert.Null(result);
    }

    /// <summary>
    /// Factory Dispose cleans up without error.
    /// </summary>
    [Fact]
    public void Dispose_NoError()
    {
        var factory = new GitServiceFactory();
        factory.Dispose();

        // Double dispose should not throw
        factory.Dispose();
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testRepoPath))
        {
            try
            {
                Directory.Delete(_testRepoPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
