using System;
using System.IO;
using System.Threading;

namespace Aero.Services.Git;

/// <summary>
/// Factory for creating IGitService instances, with caching per workspace path.
/// </summary>
public sealed class GitServiceFactory : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IGitService? _cachedService;
    private string? _cachedWorkspacePath;
    private bool _disposed;

    /// <summary>
    /// Detects whether the given path is a Git repository and returns an
    /// appropriate IGitService instance, or null if no repository exists.
    /// Result is cached for the workspace path — subsequent calls with the same
    /// path return the cached instance.
    /// </summary>
    public IGitService? Detect(string workspacePath)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GitServiceFactory));

        if (string.IsNullOrEmpty(workspacePath))
            return null;

        var normalizedPath = Path.GetFullPath(workspacePath);

        // Return cached instance if path matches
        if (_cachedService != null && string.Equals(_cachedWorkspacePath, normalizedPath, StringComparison.Ordinal))
        {
            return _cachedService;
        }

        // Look for .git directory
        var gitDir = Path.Combine(normalizedPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            // No repository at this path
            return null;
        }

        // Dispose old service if workspace changed
        if (_cachedService != null)
        {
            _cachedService.Dispose();
            _cachedService = null;
            _cachedWorkspacePath = null;
        }

        // Create new service (libgit2sharp implementation)
        // R1.2 + R1.4 fix: catch service unavailability and return null gracefully
        try
        {
            _cachedService = new LibGit2SharpService(gitDir, normalizedPath);
            _cachedWorkspacePath = normalizedPath;
            return _cachedService;
        }
        catch (GitServiceUnavailableException)
        {
            // Repository couldn't be opened - not a valid git repo
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _lock.Wait();
        try
        {
            _cachedService?.Dispose();
            _cachedService = null;
            _cachedWorkspacePath = null;
        }
        finally
        {
            _lock.Dispose();
        }
    }
}
