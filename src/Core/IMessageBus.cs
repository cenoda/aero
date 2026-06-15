using System;

namespace Aero.Core;

/// <summary>
/// Cross-cutting event bus. Services and ViewModels communicate through messages
/// rather than direct references, keeping the layers decoupled.
/// </summary>
public interface IMessageBus
{
    void Subscribe<T>(Action<T> handler);
    void Unsubscribe<T>(Action<T> handler);
    void Publish<T>(T message);
}
