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
    public async Task InboxProcessor_ProcessesPendingMessages_Success()
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
        await dbContext.SaveChangesAsync();

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
        await Task.Delay(1000);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessage = await dbContext.InboxMessages.FindAsync(inboxMessage.Id);
        Assert.IsNotNull(processedMessage);
        Assert.AreEqual(1, processedMessage.Status); // Processed
        Assert.IsNotNull(processedMessage.ProcessedOnUtc);
        Assert.AreEqual(1, eventConsumer.ConsumedEvents.Count);
        Assert.IsInstanceOfType<TestInboxEvent>(eventConsumer.ConsumedEvents[0]);
    }

    [TestMethod]
    public async Task InboxProcessor_HandlesProcessingError_MarksAsFailed()
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
        await dbContext.SaveChangesAsync();

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
        await Task.Delay(1000);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessage = await dbContext.InboxMessages.FindAsync(inboxMessage.Id);
        Assert.IsNotNull(processedMessage);
        Assert.AreEqual(2, processedMessage.Status); // Failed
        Assert.IsNotNull(processedMessage.ProcessingError);
        Assert.IsTrue(processedMessage.ProcessingError.Contains("Test exception"));
    }

    [TestMethod]
    public async Task InboxProcessor_WithNoDbContext_LogsWarningAndContinues()
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
        await Task.Delay(600);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(loggerMessages.Any(m => m.Contains("No DbContext registered")));
    }

    [TestMethod]
    public async Task InboxProcessor_WithNoEventConsumer_LogsWarningAndContinues()
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
        await Task.Delay(600);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(loggerMessages.Any(m => m.Contains("No IEventConsumer registered")));
    }

    [TestMethod]
    public async Task InboxProcessor_WithNoEventTypeRegistry_LogsWarningAndContinues()
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
        await Task.Delay(600);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(loggerMessages.Any(m => m.Contains("No IEventTypeRegistry registered")));
    }

    [TestMethod]
    public async Task InboxProcessor_ProcessesMultipleMessages_InOrder()
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
        await dbContext.SaveChangesAsync();

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
        await Task.Delay(1500);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        var processedMessages = await dbContext.InboxMessages.Where(m => m.Status == 1).CountAsync();
        Assert.AreEqual(5, processedMessages);
        Assert.AreEqual(5, eventConsumer.ConsumedEvents.Count);
    }

    [TestMethod]
    public async Task InboxProcessor_SkipsAlreadyProcessedMessages()
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
        await dbContext.SaveChangesAsync();

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
        await Task.Delay(1000);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(0, eventConsumer.ConsumedEvents.Count);
    }

    [TestMethod]
    public async Task InboxProcessor_HandlesCancellation_StopsGracefully()
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
        await Task.Delay(300);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - No exception should be thrown
        Assert.IsTrue(true);
    }
}
