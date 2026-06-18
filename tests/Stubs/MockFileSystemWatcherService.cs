using System;
using System.Collections.Generic;
using Aero.Core;
using Aero.Services;

namespace Aero.Tests.Stubs;

/// <summary>
/// Test double for <see cref="IFileSystemWatcherService"/>. Records
/// <see cref="Watch(string)"/> and <see cref="StopWatching"/> calls and lets
/// tests raise <see cref="FolderChanged"/> manually.
/// </summary>
public sealed class MockFileSystemWatcherService : IFileSystemWatcherService
{
    private readonly IMessageBus? _bus;
    private readonly List<string> _watchedPaths = new();
    private readonly List<string> _stoppedPaths = new();

    public MockFileSystemWatcherService(IMessageBus? bus = null)
    {
        _bus = bus;
    }

    /// <summary>All paths ever passed to <see cref="Watch(string)"/>, in order.</summary>
    public IReadOnlyList<string> WatchedPaths => _watchedPaths;

    /// <summary>All paths recorded when <see cref="StopWatching"/> ran, in order.</summary>
    public IReadOnlyList<string> StoppedPaths => _stoppedPaths;

    /// <summary>The most recent path passed to <see cref="Watch(string)"/>, if any.</summary>
    public string? CurrentPath { get; private set; }

    /// <inheritdoc />
    public bool IsWatching => CurrentPath != null;

    /// <inheritdoc />
    public void Watch(string path)
    {
        if (CurrentPath != null)
            _stoppedPaths.Add(CurrentPath);
        _watchedPaths.Add(path);
        CurrentPath = path;
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        if (CurrentPath != null)
            _stoppedPaths.Add(CurrentPath);
        CurrentPath = null;
    }

    /// <summary>
    /// Simulate a debounced filesystem change. Publishes
    /// <see cref="FolderChanged"/> if a bus was supplied.
    /// </summary>
    public void RaiseFolderChanged(string path)
    {
        _bus?.Publish(new FolderChanged(path));
    }

    /// <inheritdoc />
    public void Dispose() => StopWatching();
}
