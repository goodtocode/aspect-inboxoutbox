using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Services;

namespace Goodtocode.InboxOutbox.Tests.Entities;

[TestClass]
public class OutboxMessageTests
{
    [TestMethod]
    public void OutboxMessageCanBeCreated()
    {
        // Arrange
        var id = Guid.NewGuid();
        var occurredOn = DateTime.UtcNow;
        var type = "TestEvent";
        var payload = "{\"test\":\"data\"}";

        // Act
        var outboxMessage = new OutboxMessage
        {
            Id = id,
            OccurredOnUtc = occurredOn,
            Type = type,
            Payload = payload,
            Status = 0
        };

        // Assert
        Assert.AreEqual(id, outboxMessage.Id);
        Assert.AreEqual(occurredOn, outboxMessage.OccurredOnUtc);
        Assert.AreEqual(type, outboxMessage.Type);
        Assert.AreEqual(payload, outboxMessage.Payload);
        Assert.AreEqual(0, outboxMessage.Status);
    }
}
