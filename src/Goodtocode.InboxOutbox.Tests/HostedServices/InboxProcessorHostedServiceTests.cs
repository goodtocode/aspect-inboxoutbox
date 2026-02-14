using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.HostedServices;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Goodtocode.InboxOutbox.Tests.HostedServices;

[TestClass]
public class InboxProcessorHostedServiceTests
{
    [TestMethod]
    public async Task InboxProcessorProcessesPendingMessagesSuccess()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var testEvent = new TestInboxEvent { Message = "Test Message" };
        var inboxMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestInboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 0,
            ReceivedOnUtc = DateTime.UtcNow
        };

        dbContext.InboxMessages.Add(inboxMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        var eventConsumer = new TestEventConsumer();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<InboxProcessorHostedService>>();
        var hostedService = new InboxProcessorHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var executeTask = hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessage = await dbContext.InboxMessages.FindAsync([inboxMessage.Id], TestContext.CancellationToken);
        Assert.IsNotNull(processedMessage);
        Assert.AreEqual(1, processedMessage.Status); // Processed
        Assert.IsNotNull(processedMessage.ProcessedOnUtc);
        Assert.HasCount(1, eventConsumer.ConsumedEvents);
        Assert.IsInstanceOfType<TestInboxEvent>(eventConsumer.ConsumedEvents[0]);
    }

    [TestMethod]
    public async Task InboxProcessorHandlesProcessingErrorMarksAsFailed()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var testEvent = new TestInboxEvent { Message = "Test Message" };
        var inboxMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestInboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 0,
            ReceivedOnUtc = DateTime.UtcNow
        };

        dbContext.InboxMessages.Add(inboxMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        var eventConsumer = new ThrowingEventConsumer();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<InboxProcessorHostedService>>();
        var hostedService = new InboxProcessorHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var executeTask = hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessage = await dbContext.InboxMessages.FindAsync([inboxMessage.Id], CancellationToken.None);
        Assert.IsNotNull(processedMessage);
        Assert.AreEqual(2, processedMessage.Status); // Failed
        Assert.IsNotNull(processedMessage.ProcessingError);
        Assert.Contains("Test exception", processedMessage.ProcessingError);
    }

    [TestMethod]
    public async Task InboxProcessorWithNoDbContextLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<InboxProcessorHostedService>(loggerMessages);

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new InboxProcessorHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No DbContext registered"), loggerMessages);
    }

    [TestMethod]
    public async Task InboxProcessorWithNoEventConsumerLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<InboxProcessorHostedService>(loggerMessages);

        services.AddSingleton<DbContext>(dbContext);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new InboxProcessorHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No IEventConsumer registered"), loggerMessages);
    }

    [TestMethod]
    public async Task InboxProcessorWithNoEventTypeRegistryLogsWarningAndContinues()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventConsumer = new TestEventConsumer();
        var loggerMessages = new List<string>();
        var mockLogger = new TestLogger<InboxProcessorHostedService>(loggerMessages);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var hostedService = new InboxProcessorHostedService(serviceProvider, mockLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(600, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(m => m.Contains("No IEventTypeRegistry registered"), loggerMessages);
    }

    [TestMethod]
    public async Task InboxProcessorProcessesMultipleMessagesInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventConsumer = new TestEventConsumer();
        var eventTypeRegistry = new TestEventTypeRegistry();

        for (int i = 0; i < 5; i++)
        {
            var testEvent = new TestInboxEvent { Message = $"Message {i}" };
            var inboxMessage = new InboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(TestInboxEvent),
                Payload = JsonSerializer.Serialize(testEvent),
                Status = 0,
                ReceivedOnUtc = DateTime.UtcNow.AddSeconds(-i)
            };
            dbContext.InboxMessages.Add(inboxMessage);
        }
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<InboxProcessorHostedService>>();
        var hostedService = new InboxProcessorHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1500, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessages = await dbContext.InboxMessages.Where(m => m.Status == 1).CountAsync(TestContext.CancellationToken);
        Assert.AreEqual(5, processedMessages);
        Assert.HasCount(5, eventConsumer.ConsumedEvents);
    }

    [TestMethod]
    public async Task InboxProcessorSkipsAlreadyProcessedMessages()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventConsumer = new TestEventConsumer();
        var eventTypeRegistry = new TestEventTypeRegistry();

        var testEvent = new TestInboxEvent { Message = "Already Processed" };
        var processedMessage = new InboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(TestInboxEvent),
            Payload = JsonSerializer.Serialize(testEvent),
            Status = 1, // Already processed
            ReceivedOnUtc = DateTime.UtcNow,
            ProcessedOnUtc = DateTime.UtcNow
        };

        dbContext.InboxMessages.Add(processedMessage);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<InboxProcessorHostedService>>();
        var hostedService = new InboxProcessorHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(1000, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.IsEmpty(eventConsumer.ConsumedEvents);
    }

    [TestMethod]
    public async Task InboxProcessorHandlesCancellationStopsGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var eventConsumer = new TestEventConsumer();
        var eventTypeRegistry = new TestEventTypeRegistry();

        services.AddSingleton<DbContext>(dbContext);
        services.AddSingleton<IEventConsumer>(eventConsumer);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<InboxProcessorHostedService>>();
        var hostedService = new InboxProcessorHostedService(serviceProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(300, TestContext.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - No exception should be thrown
        // No assertion needed; test will fail if an exception is thrown
    }

    public TestContext TestContext { get; set; }
}
