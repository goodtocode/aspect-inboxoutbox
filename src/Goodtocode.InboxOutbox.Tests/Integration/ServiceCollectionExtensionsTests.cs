using Goodtocode.InboxOutbox.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Goodtocode.InboxOutbox.Tests.Integration;

[TestClass]
public class ServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddInboxOutboxRegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddInboxOutbox(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var eventTypeRegistry = serviceProvider.GetService<Interfaces.IEventTypeRegistry>();
        Assert.IsNotNull(eventTypeRegistry);

        var interceptor = serviceProvider.GetService<Interceptors.OutboxSaveChangesInterceptor>();
        Assert.IsNotNull(interceptor);
    }
}
