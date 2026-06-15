using System;
using System.Collections.Generic;
using Aero.Core;

namespace Aero.Tests.Stubs;

/// <summary>
/// Simple in-process stub for IMessageBus that records published messages
/// and dispatches them synchronously to subscribers.
/// </summary>
public class StubMessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    /// <summary>All messages published so far, in order.</summary>
    public List<object> Published { get; } = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var key = typeof(T);
        if (!_handlers.TryGetValue(key, out var list))
        {
            list = new List<Delegate>();
            _handlers[key] = list;
        }
        list.Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
            list.Remove(handler);
    }

    public void Publish<T>(T message)
    {
        if (message is not null)
            Published.Add(message);

        if (_handlers.TryGetValue(typeof(T), out var list))
        {
            // Copy to avoid mutation during iteration
            foreach (var handler in list.ToArray())
                ((Action<T>)handler)(message!);
        }
    }

    /// <summary>Return all published messages of a specific type.</summary>
    public IEnumerable<T> MessagesOf<T>() where T : class
    {
        foreach (var msg in Published)
            if (msg is T typed)
                yield return typed;
    }
}
