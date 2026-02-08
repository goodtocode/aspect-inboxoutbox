using Goodtocode.InboxOutbox.Entities;

namespace Goodtocode.InboxOutbox.Tests;

[TestClass]
public class InboxMessageTests
{
    [TestMethod]
    public void InboxMessage_CanBeCreated()
    {
        // Arrange
        var id = Guid.NewGuid();
        var receivedOn = DateTime.UtcNow;
        var type = "TestEvent";
        var payload = "{\"test\":\"data\"}";

        // Act
        var inboxMessage = new InboxMessage
        {
            Id = id,
            ReceivedOnUtc = receivedOn,
            Type = type,
            Payload = payload,
            Status = 0
        };

        // Assert
        Assert.AreEqual(id, inboxMessage.Id);
        Assert.AreEqual(receivedOn, inboxMessage.ReceivedOnUtc);
        Assert.AreEqual(type, inboxMessage.Type);
        Assert.AreEqual(payload, inboxMessage.Payload);
        Assert.AreEqual(0, inboxMessage.Status);
    }
}
