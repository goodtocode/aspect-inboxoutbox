namespace Goodtocode.InboxOutbox.Interfaces;

/// <summary>
/// Event publisher interface for sending events to messaging infrastructure
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : class;
}
