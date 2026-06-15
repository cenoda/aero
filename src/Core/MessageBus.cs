using System;
using System.Collections.Generic;

namespace Aero.Core;

/// <summary>
/// Simple, thread-safe in-process event bus.
/// Handlers are stored per message type and invoked synchronously on Publish.
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }
    }

    public void Publish<T>(T message)
    {
        List<Delegate> snapshot;
        lock (_lock)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
                return;
            snapshot = new List<Delegate>(list);
        }

        foreach (var handler in snapshot)
            ((Action<T>)handler)(message);
    }
}
