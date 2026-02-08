namespace Goodtocode.InboxOutbox.Interfaces;

/// <summary>
/// Domain event marker interface
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
