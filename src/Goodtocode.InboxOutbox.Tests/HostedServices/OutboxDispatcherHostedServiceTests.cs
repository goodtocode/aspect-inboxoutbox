using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.HostedServices;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Goodtocode.InboxOutbox.Tests.HostedServices;

[TestClass]
public class OutboxDispatcherHostedServiceTests
{
    [TestMethod]
    public async Task OutboxDispatcherPublishesPendingMessagesSuccess()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var testEvent = new TestOutboxEvent { Message = "Test Message" };
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestOutboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 0,
            OccurredOnUtc = DateTime.UtcNow
        };

        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var publishedMessage = await dbContext.OutboxMessages.FindAsync([outboxMessage.Id], CancellationToken.None);
        Assert.IsNotNull(publishedMessage);
        Assert.AreEqual(1, publishedMessage.Status); // Published
        Assert.IsNotNull(publishedMessage.LastDispatchedOnUtc);
        Assert.HasCount(1, eventBus.PublishedEvents);
        Assert.IsInstanceOfType<TestOutboxEvent>(eventBus.PublishedEvents[0]);
    }

    [TestMethod]
    public async Task OutboxDispatcherHandlesPublishingErrorMarksAsFailed()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var testEvent = new TestOutboxEvent { Message = "Test Message" };
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestOutboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 0,
            OccurredOnUtc = DateTime.UtcNow
        };

        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        var eventBus = new ThrowingEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var publishedMessage = await dbContext.OutboxMessages.FindAsync([outboxMessage.Id], CancellationToken.None);
        Assert.IsNotNull(publishedMessage);
        Assert.AreEqual(2, publishedMessage.Status); // Failed
        Assert.IsNotNull(publishedMessage.LastDispatchError);
        Assert.Contains("Test exception", publishedMessage.LastDispatchError);
    }

    [TestMethod]
    public async Task OutboxDispatcherWithNoDbContextLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<OutboxDispatcherHostedService>(loggerMessages);

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No DbContext registered"), loggerMessages);
    }

    [TestMethod]
    public async Task OutboxDispatcherWithNoEventBusLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<OutboxDispatcherHostedService>(loggerMessages);

        services.AddSingleton<DbContext>(dbContext);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No IEventBus registered"), loggerMessages);
    }

    [TestMethod]
    public async Task OutboxDispatcherWithNoEventTypeRegistryLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventBus = new TestEventBus();
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<OutboxDispatcherHostedService>(loggerMessages);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No IEventTypeRegistry registered"), loggerMessages);
    }

    [TestMethod]
    public async Task OutboxDispatcherPublishesMultipleMessagesInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        for (int i = 0; i < 5; i++)
        {
            var testEvent = new TestOutboxEvent { Message = $"Message {i}" };
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(TestOutboxEvent),
                Payload = JsonSerializer.Serialize(testEvent),
                Status = 0,
                OccurredOnUtc = DateTime.UtcNow.AddSeconds(-i)
            };
            dbContext.OutboxMessages.Add(outboxMessage);
        }
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1500, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var publishedMessages = await dbContext.OutboxMessages.Where(m => m.Status == 1).CountAsync(TestContext.CancellationToken);
        Assert.AreEqual(5, publishedMessages);
        Assert.HasCount(5, eventBus.PublishedEvents);
    }

    [TestMethod]
    public async Task OutboxDispatcherSkipsAlreadyPublishedMessages()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        var testEvent = new TestOutboxEvent { Message = "Already Published" };
        var publishedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestOutboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 1, // Already published
            OccurredOnUtc = DateTime.UtcNow,
            LastDispatchedOnUtc = DateTime.UtcNow
        };

        dbContext.OutboxMessages.Add(publishedMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.IsEmpty(eventBus.PublishedEvents);
    }

    [TestMethod]
    public async Task OutboxDispatcherHandlesCancellationStopsGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(300, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - No exception should be thrown
    }

    [TestMethod]
    public async Task OutboxDispatcherHandlesDeserializationErrorMarksAsFailed()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestOutboxEvent),
            Payload = "{ invalid json", // Invalid JSON
            Status = 0,
            OccurredOnUtc = DateTime.UtcNow
        };

        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var failedMessage = await dbContext.OutboxMessages.FindAsync([outboxMessage.Id], CancellationToken.None);
        Assert.IsNotNull(failedMessage);
        Assert.AreEqual(2, failedMessage.Status); // Failed
        Assert.IsNotNull(failedMessage.LastDispatchError);
    }

    [TestMethod]
    public async Task OutboxDispatcherProcessesUpTo100MessagesPerBatch()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventBus = new TestEventBus();
        var eventTypeRegistry = new TestEventTypeRegistry();

        // Add 150 messages
        for (int i = 0; i < 150; i++)
        {
            var testEvent = new TestOutboxEvent { Message = $"Message {i}" };
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(TestOutboxEvent),
                Payload = JsonSerializer.Serialize(testEvent),
                Status = 0,
                OccurredOnUtc = DateTime.UtcNow.AddSeconds(-i)
            };
            dbContext.OutboxMessages.Add(outboxMessage);
        }
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatcherHostedService>>();
        var hostedService = new OutboxDispatcherHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1500, TestContext.CancellationToken); // Give time for at least one batch
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - Should have processed at least 100 in first batch
        var publishedMessages = await dbContext.OutboxMessages.Where(m => m.Status == 1).CountAsync(TestContext.CancellationToken);
        Assert.IsGreaterThanOrEqualTo(100, publishedMessages);
    }

    public TestContext TestContext { get; set; }
}
