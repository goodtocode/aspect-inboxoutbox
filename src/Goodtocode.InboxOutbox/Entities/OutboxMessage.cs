namespace Goodtocode.InboxOutbox.Entities;

/// <summary>
/// Represents an outbox message that stores unpublished domain events
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime? LastDispatchedOnUtc { get; set; }
    public string? LastDispatchError { get; set; }
}
