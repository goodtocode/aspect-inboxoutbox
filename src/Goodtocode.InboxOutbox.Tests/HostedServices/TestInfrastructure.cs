using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Extensions;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Goodtocode.InboxOutbox.Tests.HostedServices;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyInboxOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

public class TestInboxEvent
{
    public string Message { get; set; } = string.Empty;
}

public class TestOutboxEvent
{
    public string Message { get; set; } = string.Empty;
}

public class TestEventConsumer : IEventConsumer
{
    public List<object> ConsumedEvents { get; } = [];

    public Task ConsumeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        ConsumedEvents.Add(@event);
        return Task.CompletedTask;
    }
}

public class ThrowingEventConsumer : IEventConsumer
{
    public Task ConsumeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        throw new InvalidOperationException("Test exception in event consumer");
    }
}

public class TestEventBus : IEventBus
{
    public List<object> PublishedEvents { get; } = [];

    public Task PublishAsync(object eventData, CancellationToken cancellationToken = default)
    {
        PublishedEvents.Add(eventData);
        return Task.CompletedTask;
    }
}

public class ThrowingEventBus : IEventBus
{
    public Task PublishAsync(object eventData, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Test exception in event bus");
    }
}

public class TestEventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<string, Type> _registry = new()
    {
        { nameof(TestInboxEvent), typeof(TestInboxEvent) },
        { nameof(TestOutboxEvent), typeof(TestOutboxEvent) }
    };

    public void Register<TEvent>() where TEvent : class
    {
        _registry[typeof(TEvent).Name] = typeof(TEvent);
    }

    public void Register(Type eventType)
    {
        _registry[eventType.Name] = eventType;
    }

    public Type Resolve(string eventTypeName)
    {
        return _registry.TryGetValue(eventTypeName, out var type)
            ? type
            : throw new InvalidOperationException($"Event type '{eventTypeName}' not registered");
    }
}

public class TestLogger<T>(List<string> logMessages) : ILogger<T>
{
    private readonly List<string> _logMessages = logMessages;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logMessages.Add(formatter(state, exception));
    }
}
