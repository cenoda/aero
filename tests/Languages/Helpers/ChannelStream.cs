using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Tests.Languages.Helpers;

internal sealed class ChannelStream
{
    private readonly ConcurrentQueue<byte[]> _segments = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _gate = new();
    private byte[]? _currentSegment;
    private int _currentOffset;
    private bool _isCompleted;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_gate)
            {
                if (_currentSegment != null)
                {
                    var remaining = _currentSegment.Length - _currentOffset;
                    var copyLength = Math.Min(buffer.Length, remaining);
                    _currentSegment.AsMemory(_currentOffset, copyLength).CopyTo(buffer);
                    _currentOffset += copyLength;

                    if (_currentOffset >= _currentSegment.Length)
                    {
                        _currentSegment = null;
                        _currentOffset = 0;
                    }

                    return copyLength;
                }

                if (_segments.TryDequeue(out var nextSegment))
                {
                    _currentSegment = nextSegment;
                    _currentOffset = 0;
                    continue;
                }

                if (_isCompleted)
                {
                    return 0;
                }
            }

            await _signal.WaitAsync(cancellationToken);
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (buffer.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        _segments.Enqueue(buffer.ToArray());
        _signal.Release();
        return ValueTask.CompletedTask;
    }

    public void Complete()
    {
        lock (_gate)
        {
            _isCompleted = true;
        }

        _signal.Release();
    }
}
