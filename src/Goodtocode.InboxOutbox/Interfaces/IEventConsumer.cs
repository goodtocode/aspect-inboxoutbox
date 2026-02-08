namespace Goodtocode.InboxOutbox.Interfaces;

/// <summary>
/// Event consumer interface for receiving events from messaging infrastructure
/// </summary>
public interface IEventConsumer
{
    Task ConsumeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
