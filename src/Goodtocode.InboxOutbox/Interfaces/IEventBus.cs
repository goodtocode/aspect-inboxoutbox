namespace Goodtocode.InboxOutbox.Interfaces;

/// <summary>
/// Event bus abstraction for publishing and consuming events
/// </summary>
public interface IEventBus
{
    Task PublishAsync(object @event, CancellationToken cancellationToken = default);
}
