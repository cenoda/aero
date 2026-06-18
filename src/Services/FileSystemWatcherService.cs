using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Aero.Core;

namespace Aero.Services;

/// <summary>
/// Default <see cref="IFileSystemWatcherService"/> built on
/// <see cref="System.IO.FileSystemWatcher"/>.
///
/// Behaviour:
/// <list type="bullet">
///   <item>Watches exactly one folder at a time with
///         <c>IncludeSubdirectories = true</c>.</item>
///   <item>Raw Created/Deleted/Renamed/Changed events are filtered through
///         <see cref="IIgnoreList.IsIgnored"/> before arming a debounce timer.</item>
///   <item>After the last filtered event, a <see cref="TimeSpan"/> quiet period
///         elapses before <see cref="FolderChanged"/> is published.</item>
///   <item>If the OS watcher raises <see cref="FileSystemWatcher.Error"/>,
///         watching stops, a <see cref="StatusMessage"/> is published, and
///         manual refresh remains available.</item>
/// </list>
/// </summary>
public sealed class FileSystemWatcherService : IFileSystemWatcherService
{
    private readonly IMessageBus _bus;
    private readonly IIgnoreList _ignoreList;
    private readonly TimeSpan _debounceWindow;
    private readonly object _lock = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private string? _rootPath;
    private bool _isDisposed;

    /// <summary>
    /// Creates the watcher service.
    /// </summary>
    /// <param name="bus">Message bus for <see cref="FolderChanged"/> and
    /// <see cref="StatusMessage"/> publications.</param>
    /// <param name="ignoreList">Ignore list used to filter raw events.</param>
    /// <param name="debounceMilliseconds">Quiet period after the last raw
    /// event before publishing <see cref="FolderChanged"/>. Default 300 ms.</param>
    public FileSystemWatcherService(
        IMessageBus bus,
        IIgnoreList ignoreList,
        int debounceMilliseconds = 300)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _ignoreList = ignoreList ?? throw new ArgumentNullException(nameof(ignoreList));
        if (debounceMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(debounceMilliseconds));

        _debounceWindow = TimeSpan.FromMilliseconds(debounceMilliseconds);
    }

    /// <inheritdoc />
    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return !_isDisposed && _watcher != null;
            }
        }
    }

    /// <inheritdoc />
    public void Watch(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        lock (_lock)
        {
            ThrowIfDisposed();
            StopWatchingCore();

            _rootPath = path;
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.CreationTime,
            };

            _watcher.Created += OnCreatedDeletedChanged;
            _watcher.Deleted += OnCreatedDeletedChanged;
            _watcher.Changed += OnCreatedDeletedChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        lock (_lock)
        {
            StopWatchingCore();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            StopWatchingCore();
        }
    }

    // --- event handlers --------------------------------------------------

    private void OnCreatedDeletedChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (ShouldIgnore(e.FullPath))
                return;

            ArmDebounce();
        }
        catch (Exception ex)
        {
            // Never let an exception escape the FileSystemWatcher callback.
            Debug.WriteLine($"[FileSystemWatcherService] Event handler failed: {ex}");
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            // Filter on the new path — that is the state the tree will display.
            if (ShouldIgnore(e.FullPath))
                return;

            ArmDebounce();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileSystemWatcherService] Renamed handler failed: {ex}");
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        try
        {
            var detail = e.GetException()?.Message ?? "internal error";
            StopWatching();
            _bus.Publish(new StatusMessage(
                $"File system watcher stopped: {detail}. Manual refresh is still available."));
        }
        catch (Exception ex)
        {
            // The watcher itself has failed; do not let this cascade.
            Debug.WriteLine($"[FileSystemWatcherService] Error handler failed: {ex}");
        }
    }

    // --- debounce --------------------------------------------------------

    private void ArmDebounce()
    {
        lock (_lock)
        {
            if (_isDisposed || _rootPath == null)
                return;

            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                static state =>
                {
                    if (state is FileSystemWatcherService self)
                        self.OnDebounceElapsed();
                },
                this,
                _debounceWindow,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed()
    {
        string? path;
        lock (_lock)
        {
            if (_isDisposed || _rootPath == null)
                return;
            path = _rootPath;
        }

        try
        {
            _bus.Publish(new FolderChanged(path));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileSystemWatcherService] Publish failed: {ex}");
        }
    }

    // --- helpers ---------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> when the raw event path should be ignored. Deleted
    /// items may no longer exist, so <see cref="Directory.Exists"/> can fail;
    /// in that case we fall back to treating the path as a file, which still
    /// lets directory patterns match ancestor segments (e.g.
    /// <c>/repo/bin/Debug/app.dll</c> is ignored because <c>bin</c> is an
    /// ancestor).
    /// </summary>
    private bool ShouldIgnore(string fullPath)
    {
        bool isDirectory;
        try
        {
            isDirectory = Directory.Exists(fullPath);
        }
        catch
        {
            isDirectory = false;
        }

        return _ignoreList.IsIgnored(fullPath, isDirectory);
    }

    private void StopWatchingCore()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreatedDeletedChanged;
            _watcher.Deleted -= OnCreatedDeletedChanged;
            _watcher.Changed -= OnCreatedDeletedChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _rootPath = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(FileSystemWatcherService));
    }
}
