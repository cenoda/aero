using System;

namespace Aero.Services;

/// <summary>
/// Watches a single workspace folder and publishes a debounced
/// <see cref="Aero.Core.FolderChanged"/> message when the file system changes.
/// Only one folder is watched at a time; calling <see cref="Watch(string)"/>
/// replaces the previous watcher.
/// </summary>
public interface IFileSystemWatcherService : IDisposable
{
    /// <summary>Start watching <paramref name="path"/>, stopping any previous watcher first.</summary>
    void Watch(string path);

    /// <summary>Stop watching the current folder without disposing the service.</summary>
    void StopWatching();

    /// <summary>Whether a watcher is currently active.</summary>
    bool IsWatching { get; }
}
