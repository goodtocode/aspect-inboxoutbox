# Goodtocode.InboxOutbox Implementation Guide

## Overview

This guide provides detailed information on implementing the Inbox/Outbox pattern using the Goodtocode.InboxOutbox library.

## Architecture

The Inbox/Outbox pattern ensures reliable event publishing and processing in distributed systems by:

1. **Outbox Pattern**: Storing domain events in the same transaction as business data
2. **Inbox Pattern**: Ensuring idempotent processing of received events
3. **Background Workers**: Asynchronously publishing and processing messages

## Components

### 1. Core Entities

- **OutboxMessage**: Stores unpublished domain events
  - `Id`: Unique identifier
  - `OccurredOnUtc`: When the event occurred
  - `Type`: Event type name for deserialization
  - `Payload`: JSON serialized event data
  - `Status`: 0=Pending, 1=Published, 2=Failed
  - `LastDispatchedOnUtc`: When last published
  - `LastDispatchError`: Error details if failed

- **InboxMessage**: Stores received events
  - `Id`: Unique identifier (from message)
  - `ReceivedOnUtc`: When received
  - `Type`: Event type name
  - `Payload`: JSON serialized event
  - `Status`: 0=Pending, 1=Processed, 2=Failed
  - `ProcessedOnUtc`: When processed
  - `ProcessingError`: Error details if failed

### 2. Interfaces

- **IDomainEvent**: Marker interface for domain events
- **IDomainEntity**: Interface for entities that raise events
- **IEventBus**: Abstraction for publishing events
- **IEventPublisher**: Publishing interface
- **IEventConsumer**: Consumption interface
- **IEventTypeRegistry**: Type resolution for serialization

### 3. Infrastructure Components

- **OutboxSaveChangesInterceptor**: EF Core interceptor that captures domain events
- **OutboxDispatcherHostedService**: Background service that publishes outbox messages
- **InboxProcessorHostedService**: Background service that processes inbox messages
- **DefaultEventTypeRegistry**: Default type registry implementation

## Implementation Steps

### Step 1: Add Package Reference

```bash
dotnet add package Goodtocode.InboxOutbox
```

### Step 2: Configure DbContext

```csharp
public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) 
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply inbox/outbox configurations
        modelBuilder.ApplyInboxOutbox();
        
        base.OnModelCreating(modelBuilder);
    }
}
```

### Step 3: Register Services

```csharp
// In Program.cs or Startup.cs
services.AddDbContext<YourDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
    
    // Add the outbox interceptor
    var interceptor = serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>();
    options.AddInterceptors(interceptor);
});

// Register inbox/outbox infrastructure
services.AddInboxOutbox(configuration);

// Register your event bus implementation
services.AddSingleton<IEventBus, YourEventBusImplementation>();

// Register your event consumer
services.AddSingleton<IEventConsumer, YourEventConsumer>();
```

### Step 4: Create Domain Entities

```csharp
public class Order : IDomainEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void Complete()
    {
        Status = OrderStatus.Completed;
        _domainEvents.Add(new OrderCompletedEvent(Id, OrderNumber));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Step 5: Define Domain Events

```csharp
public class OrderCompletedEvent : IDomainEvent
{
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public DateTime OccurredOnUtc { get; }

    public OrderCompletedEvent(Guid orderId, string orderNumber)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
```

### Step 6: Register Event Types

```csharp
// Register event types for serialization/deserialization
var eventTypeRegistry = serviceProvider.GetRequiredService<IEventTypeRegistry>();
eventTypeRegistry.Register<OrderCompletedEvent>();
eventTypeRegistry.Register<OrderCancelledEvent>();
// ... register all your event types
```

### Step 7: Add Migrations

```bash
dotnet ef migrations add AddInboxOutbox
dotnet ef database update
```

## Event Flow

### Outbox Flow (Publishing)

1. Command handler loads aggregate
2. Aggregate raises domain events (via `AddDomainEvent`)
3. Command handler calls `SaveChangesAsync()`
4. EF interceptor picks up domain events
5. Interceptor writes events to OutboxMessages table
6. Interceptor clears domain events from aggregate
7. Transaction commits (both business data and outbox)
8. Background worker queries outbox for pending messages
9. Worker publishes events to message bus
10. Worker marks messages as published

### Inbox Flow (Consuming)

1. Message received from message bus
2. Write to InboxMessages table with unique message ID
3. Background worker queries inbox for pending messages
4. Worker deserializes event using event type registry
5. Worker passes event to IEventConsumer
6. Consumer processes event
7. Worker marks message as processed

## Best Practices

### 1. Transaction Boundaries
- Always ensure outbox writes are in the same transaction as business data
- Use EF Core's transaction management or explicit transactions

### 2. Idempotency
- Use message ID as InboxMessage.Id to detect duplicates
- Handle duplicate processing gracefully in consumers

### 3. Error Handling
- Failed messages are marked with Status=2
- Implement retry logic with exponential backoff
- Monitor failed messages and alert on consistent failures

### 4. Performance
- Index on (Status, OccurredOnUtc) for outbox queries
- Index on (Status, ReceivedOnUtc) for inbox queries
- Batch message processing (default: 100 messages)
- Tune worker intervals based on throughput needs

### 5. Monitoring
- Track outbox message age
- Monitor failed message count
- Alert on processing delays
- Log all publishing and processing operations

### 6. Event Type Registry
- Register all event types at startup
- Use consistent type names across services
- Consider versioning strategy for event evolution

## Azure Service Bus Integration Example

```csharp
public class AzureServiceBusEventBus : IEventBus
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<AzureServiceBusEventBus> _logger;

    public AzureServiceBusEventBus(
        ServiceBusClient client,
        IConfiguration configuration,
        ILogger<AzureServiceBusEventBus> logger)
    {
        var topicName = configuration["ServiceBus:TopicName"];
        _sender = client.CreateSender(topicName);
        _logger = logger;
    }

    public async Task PublishAsync(object @event, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(@event, @event.GetType());
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = @event.GetType().Name
        };

        await _sender.SendMessageAsync(message, cancellationToken);
        _logger.LogInformation("Published event {EventType}", @event.GetType().Name);
    }
}
```

## Troubleshooting

### Messages not being published
- Check that OutboxDispatcherHostedService is registered
- Verify IEventBus is registered
- Check logs for errors
- Verify database connectivity

### Messages not being processed
- Check that InboxProcessorHostedService is registered
- Verify IEventConsumer is registered
- Ensure event types are registered in IEventTypeRegistry
- Check logs for deserialization errors

### Duplicate processing
- Ensure InboxMessage.Id matches message ID from bus
- Verify duplicate detection logic in consumer

## Advanced Scenarios

### Custom Event Type Registry

```csharp
public class CustomEventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<string, Type> _typeMap = new();

    public Type Resolve(string eventTypeName)
    {
        // Custom resolution logic
        // e.g., handle versioned events
        return _typeMap[eventTypeName];
    }

    public void Register<TEvent>() where TEvent : class
    {
        // Custom registration logic
    }

    public void Register(Type eventType)
    {
        // Custom registration logic
    }
}
```

### Multiple Bounded Contexts

Each bounded context should have its own:
- DbContext with ApplyInboxOutbox()
- Set of InboxMessages and OutboxMessages tables
- Background workers (or shared with proper scoping)

### Event Versioning

Consider including version in event type:
```csharp
public class OrderCompletedEvent_V2 : IDomainEvent
{
    // New fields...
}
```

Register both versions and handle in consumer:
```csharp
eventTypeRegistry.Register<OrderCompletedEvent>();
eventTypeRegistry.Register<OrderCompletedEvent_V2>();
```

## Resources

- [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Domain Events](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [EF Core Interceptors](https://docs.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)
