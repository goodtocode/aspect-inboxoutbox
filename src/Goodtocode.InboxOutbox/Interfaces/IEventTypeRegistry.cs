namespace Goodtocode.InboxOutbox.Interfaces;

/// <summary>
/// Interface for event type resolution for serialization/deserialization
/// </summary>
public interface IEventTypeRegistry
{
    Type Resolve(string eventTypeName);
    void Register<TEvent>() where TEvent : class;
    void Register(Type eventType);
}
