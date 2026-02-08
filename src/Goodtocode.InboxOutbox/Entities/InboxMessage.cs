namespace Goodtocode.InboxOutbox.Entities;

/// <summary>
/// Represents an inbox message for idempotent processing of received events
/// </summary>
public class InboxMessage
{
    public Guid Id { get; set; }
    public DateTime ReceivedOnUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? ProcessingError { get; set; }
}
