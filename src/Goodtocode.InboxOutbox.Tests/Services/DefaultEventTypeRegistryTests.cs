using Goodtocode.InboxOutbox.Services;

namespace Goodtocode.InboxOutbox.Tests.Services;

[TestClass]
public class DefaultEventTypeRegistryTests
{
    [TestMethod]
    public void RegisterAndResolveEventTypeSuccess()
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
    public void ResolveUnregisteredEventTypeThrowsException()
    {
        // Arrange
        var registry = new DefaultEventTypeRegistry();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => registry.Resolve("UnknownEvent"));
    }

    [TestMethod]
    public void RegisterSameEventTypeTwiceDoesNotThrow()
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
