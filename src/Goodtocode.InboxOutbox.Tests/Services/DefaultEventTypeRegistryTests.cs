using Goodtocode.InboxOutbox.Services;

namespace Goodtocode.InboxOutbox.Tests;

[TestClass]
public class DefaultEventTypeRegistryTests
{
    [TestMethod]
    public void Register_AndResolve_EventType_Success()
    {
        // Arrange
        var registry = new DefaultEventTypeRegistry();
        
        // Act
        registry.Register<TestEvent>();
        var resolvedType = registry.Resolve(nameof(TestEvent));

        // Assert
        Assert.IsNotNull(resolvedType);
        Assert.AreEqual(typeof(TestEvent), resolvedType);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Resolve_UnregisteredEventType_ThrowsException()
    {
        // Arrange
        var registry = new DefaultEventTypeRegistry();

        // Act & Assert
        registry.Resolve("UnknownEvent");
    }

    [TestMethod]
    public void Register_SameEventTypeTwice_DoesNotThrow()
    {
        // Arrange
        var registry = new DefaultEventTypeRegistry();

        // Act
        registry.Register<TestEvent>();
        registry.Register<TestEvent>();
        var resolvedType = registry.Resolve(nameof(TestEvent));

        // Assert
        Assert.IsNotNull(resolvedType);
        Assert.AreEqual(typeof(TestEvent), resolvedType);
    }
}

public class TestEvent
{
    public string Message { get; set; } = string.Empty;
}
