using System;
using System.IO;
using System.Threading;

namespace Aero.Services.Git;

/// <summary>
/// A focused <see cref="FileSystemWatcher"/> on the .git/ directory, watching
/// only HEAD, index, and COMMIT_EDITMSG for changes. Fires a provided callback
/// after a 500ms debounce window.
///
/// Design decisions (see EXTENSIONS.md):
///   D-W1: Watch .git/ not workspace root
///   D-W2: Filter to three files: HEAD, index, COMMIT_EDITMSG
///   D-W3: Plain class, not a DI service — GitViewModel owns the lifetime
///   D-W4: Uses existing 1-second cooldown in GitViewModel
///
/// Thread safety: All instance access is serialized via _lock.
/// Dispose is race-safe (R3.6): the watcher is stopped first, the debounce
/// timer is cancelled, and the callback is nulled atomically so a racing
/// timer invocation exits cleanly.
/// </summary>
public sealed class GitWatcher : IDisposable
{
    private readonly string _gitDir;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);
    private readonly object _lock = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private Action? _callback;
    private bool _disposed;

    /// <summary>
    /// True if the underlying FileSystemWatcher was successfully created and
    /// is actively watching. False if creation failed (e.g. inotify limit on
    /// Linux — see R3.5) or after Dispose.
    /// </summary>
    public bool IsWatching { get; private set; }

    /// <summary>
    /// Creates a GitWatcher for the given .git directory.
    /// If the FileSystemWatcher cannot be created (e.g., inotify limit reached
    /// on Linux — R3.5), IsWatching is set to false and no exception is thrown.
    /// </summary>
    /// <param name="gitDir">The path to the .git directory (not the workspace root).</param>
    /// <param name="onChanged">Callback invoked after debounce when a watched file changes.</param>
    public GitWatcher(string gitDir, Action onChanged)
    {
        if (string.IsNullOrEmpty(gitDir))
            throw new ArgumentException("Git directory path must not be empty.", nameof(gitDir));
        _gitDir = gitDir;
        _callback = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

        try
        {
            _watcher = new FileSystemWatcher(gitDir)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            };

            // Filter to meaningful git state files only (D-W2)
            _watcher.Created += OnGitFileChanged;
            _watcher.Changed += OnGitFileChanged;

            IsWatching = true;
        }
        catch (IOException ex)
        {
            // R3.5: inotify limit or other OS-level watcher failure.
            // Degrade gracefully — the Git panel still works, just won't auto-reload.
            IsWatching = false;
            _callback = null;
            System.Diagnostics.Debug.WriteLine(
                $"[GitWatcher] Failed to create watcher for {gitDir}: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // Path doesn't exist or is invalid
            IsWatching = false;
            _callback = null;
            System.Diagnostics.Debug.WriteLine(
                $"[GitWatcher] Invalid path {gitDir}: {ex.Message}");
        }
    }


    /// <summary>
    /// Filters raw FileSystemWatcher events to the three meaningful files
    /// (HEAD, index, COMMIT_EDITMSG) and arms the debounce timer.
    /// </summary>
    private void OnGitFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Only react to the three meaningful git state files (D-W2)
            var fileName = Path.GetFileName(e.Name ?? string.Empty);
            if (fileName is not ("HEAD" or "index" or "COMMIT_EDITMSG"))
                return;

            ArmDebounce();
        }
        // W1 fix: Only catch non-fatal exceptions
        catch (Exception ex) when (ex is not (OutOfMemoryException or
            StackOverflowException or AccessViolationException))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[GitWatcher] Event handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Arms a 500ms debounce timer. If the timer is already armed, it is
    /// reset (the previous timer is cancelled).
    /// </summary>
    private void ArmDebounce()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                static state =>
                {
                    if (state is GitWatcher self)
                        self.OnDebounceElapsed();
                },
                this,
                _debounceInterval,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Called when the debounce timer elapses. Invokes the callback if it
    /// has not been nulled by Dispose (R3.6).
    /// </summary>
    private void OnDebounceElapsed()
    {
        // R3.6: Atomically read the callback — null means Dispose was called.
        // CompareExchange(value, null, null) is a thread-safe read that
        // returns the current value without modifying it.
        var cb = Interlocked.CompareExchange(ref _callback, null, null);
        if (cb != null)
        {
            cb();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        // 1. Stop the watcher first — prevents new events from arming debounce
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnGitFileChanged;
            _watcher.Changed -= OnGitFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        // 2. Cancel/dispose the debounce timer
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        // 3. Atomically null the callback — racing timer invocation sees null (R3.6)
        Interlocked.Exchange(ref _callback, null);

        IsWatching = false;
    }
}
