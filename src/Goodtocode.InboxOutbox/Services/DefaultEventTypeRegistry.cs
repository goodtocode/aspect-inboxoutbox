using Goodtocode.InboxOutbox.Interfaces;
using System.Collections.Concurrent;

namespace Goodtocode.InboxOutbox.Services;

/// <summary>
/// Default implementation of event type registry for type mapping during serialization/deserialization
/// </summary>
public class DefaultEventTypeRegistry : IEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _typeMap = new();

    public Type Resolve(string eventTypeName)
    {
        if (_typeMap.TryGetValue(eventTypeName, out var type))
        {
            return type;
        }

        throw new InvalidOperationException($"Event type '{eventTypeName}' is not registered. " +
            "Please register the event type using IEventTypeRegistry.Register<TEvent>() or Register(Type).");
    }

    public void Register<TEvent>() where TEvent : class
    {
        Register(typeof(TEvent));
    }

    public void Register(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        _typeMap.TryAdd(eventType.Name, eventType);
    }
}
