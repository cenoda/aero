using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Tests.Stubs;

/// <summary>
/// Runs an async delegate on a single dedicated thread by installing a pumping
/// <see cref="SynchronizationContext"/> so that every <c>await</c> continuation
/// resumes on the same thread that started the work.
///
/// AvaloniaEdit's <c>TextDocument</c> is thread-affine — it must be created and
/// accessed on one thread. Plain xUnit async tests hop between thread-pool threads
/// across awaits, which would trip the document's access check. Wrapping the test
/// body in <see cref="Run"/> keeps document creation and access on a single thread.
///
/// Based on the well-known single-threaded async pump pattern.
/// </summary>
public static class SingleThread
{
    public static void Run(Func<Task> asyncMethod)
    {
        if (asyncMethod == null) throw new ArgumentNullException(nameof(asyncMethod));

        var previous = SynchronizationContext.Current;
        using var context = new PumpingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = asyncMethod();
            // Stop pumping once the root task finishes (success or failure).
            task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);
            context.Pump();
            task.GetAwaiter().GetResult(); // observe exceptions / cancellation
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class PumpingSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback callback, object? state)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("Synchronous Send is not supported on the single-thread pump.");

        public void Pump()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                callback(state);
        }

        public void Complete() => _queue.CompleteAdding();

        public void Dispose() => _queue.Dispose();
    }
}
