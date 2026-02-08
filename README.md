# Goodtocode.InboxOutbox

Infrastructure library for implementing the Inbox/Outbox pattern for event-driven architecture with .NET

[![NuGet CI/CD](https://github.com/goodtocode/aspect-inboxoutbox/actions/workflows/gtc-inboxoutbox-nuget.yml/badge.svg)](https://github.com/goodtocode/aspect-inboxoutbox/actions/workflows/gtc-inboxoutbox-nuget.yml)

Goodtocode.InboxOutbox provides a complete infrastructure solution for implementing the Inbox/Outbox messaging pattern to support Event-Driven Architecture (EDA). The library integrates seamlessly with Azure Service Bus, Event Grid, and Event Hub, with primary focus on Service Bus scenarios. It ensures reliable event publishing and processing through transactional outbox and idempotent inbox patterns.

## Features
- Transactional outbox pattern for guaranteed event publishing
- Idempotent inbox pattern for duplicate detection
- EF Core interceptor for automatic outbox message capture
- Background workers for outbox dispatch and inbox processing
- Event type registry for serialization/deserialization
- Per-bounded-context DbContext support
- Compatible with Azure Service Bus, Event Grid, and Event Hub
- Built for eventual consistency and cross-context communication
- Lightweight, extensible, and follows DDD principles

## Quick-Start Steps
1. Clone this repository
   ```
   git clone https://github.com/goodtocode/aspect-inboxoutbox.git
   ```
2. Install .NET SDK (latest recommended)
   ```
   winget install Microsoft.DotNet.SDK --silent
   ```
3. Build the solution
   ```
   cd src
   dotnet build Goodtocode.InboxOutbox.sln
   ```
4. Run tests
   ```
   cd Goodtocode.InboxOutbox.Tests
   dotnet test
   ```

## Install Prerequisites
- [.NET SDK (latest)](https://dotnet.microsoft.com/en-us/download)
- Visual Studio (latest) or VS Code

## Installation & Usage

### 1. Add the NuGet Package
```bash
dotnet add package Goodtocode.InboxOutbox
```

### 2. Add EF Configuration
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyInboxOutbox(); // from SDK
    base.OnModelCreating(modelBuilder);
}
```

### 3. Add Services in Program.cs
```csharp
services.AddInboxOutbox(configuration);
```

### 4. Add Migrations
```bash
dotnet ef migrations add AddInboxOutbox
dotnet ef database update
```

## Expected DbContext Structure (per bounded context)

```
AccountingDbContext
 - InboxMessage
 - OutboxMessage
 - Projection tables

IdentityDbContext
 - InboxMessage
 - OutboxMessage
 - Projection tables

BillingDbContext
 - InboxMessage
 - OutboxMessage
 - Projection tables
```

## SDK Components

### 1. Entities
- `OutboxMessage` - Stores unpublished domain events
- `InboxMessage` - Stores received events for idempotent processing

### 2. EF Configurations
- `OutboxMessageConfiguration` - Entity configuration for outbox table
- `InboxMessageConfiguration` - Entity configuration for inbox table

### 3. Interceptors
- `OutboxSaveChangesInterceptor` - Automatically captures domain events and writes to outbox

### 4. Hosted Services
- `OutboxDispatcherHostedService` - Background worker that publishes outbox messages
- `InboxProcessorHostedService` - Background worker that processes inbox messages

### 5. Event Serialization Helpers
- `IEventTypeRegistry` - Interface for event type resolution
- `DefaultEventTypeRegistry` - Default implementation for type mapping

### 6. Interfaces
- `IEventBus` - Event bus abstraction
- `IEventPublisher` - Event publishing interface
- `IEventConsumer` - Event consumption interface

### 7. Extension Methods
- `modelBuilder.ApplyInboxOutbox()` - Applies inbox/outbox EF configurations
- `services.AddInboxOutbox()` - Registers all required services

## Top Use Case Examples

### 1. Outbox SaveChanges Interceptor
The interceptor automatically captures domain events during `SaveChangesAsync` and writes them to the outbox table:

```csharp
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = (DbContext)eventData.Context!;
        var domainEntities = context.ChangeTracker
            .Entries<DomainEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        foreach (var entry in domainEntities)
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    Type = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent),
                    Status = 0
                };

                context.Add(outbox);
            }

            entry.Entity.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

### 2. Domain Entity Base Class
Domain entities raise events that are captured by the outbox interceptor:

```csharp
public abstract class DomainEntity
{
    private readonly List<IDomainEvent> _events = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;
    protected void AddDomainEvent(IDomainEvent e) => _events.Add(e);
    public void ClearDomainEvents() => _events.Clear();
}
```

### 3. Command Handler Flow
The complete flow from command to event publishing:

```csharp
// 1. Command handler loads aggregate
var order = await _repository.GetByIdAsync(command.OrderId);

// 2. Aggregate raises domain events
order.Complete();  // Internally calls AddDomainEvent(new OrderCompletedEvent(order))

// 3. Command handler calls SaveChangesAsync()
await _dbContext.SaveChangesAsync();

// 4. EF interceptor picks up domain events
// 5. Interceptor writes them to Outbox
// 6. Interceptor clears domain events
// 7. Transaction commits
// 8. Background worker publishes events later
```

### 4. Outbox Dispatcher Hosted Service
Background service that processes and publishes outbox messages:

```csharp
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _provider;

    public OutboxDispatcherHostedService(IServiceProvider provider)
    {
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YourDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            var messages = await db.OutboxMessages
                .Where(x => x.Status == 0)
                .OrderBy(x => x.OccurredOnUtc)
                .Take(100)
                .ToListAsync(stoppingToken);

            foreach (var msg in messages)
            {
                try
                {
                    var type = _eventTypeRegistry.Resolve(msg.Type);
                    var @event = JsonSerializer.Deserialize(msg.Payload, type);

                    await bus.PublishAsync(@event!);

                    msg.Status = 1;
                    msg.LastDispatchedOnUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.Status = 2;
                    msg.LastDispatchError = ex.ToString();
                }
            }

            await db.SaveChangesAsync(stoppingToken);

            await Task.Delay(500, stoppingToken);
        }
    }
}
```

### 5. DbContext Example with Inbox/Outbox
```csharp
public class AccountingDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    
    // Your domain entities
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyInboxOutbox(); // Applies inbox/outbox configurations
        base.OnModelCreating(modelBuilder);
    }
}
```

## Technologies
- [C# .NET](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Azure Service Bus](https://docs.microsoft.com/en-us/azure/service-bus-messaging/)
- [Azure Event Grid](https://docs.microsoft.com/en-us/azure/event-grid/)
- [Azure Event Hub](https://docs.microsoft.com/en-us/azure/event-hubs/)

## Version History

| Version | Date        | Release Notes                                    |
|---------|-------------|--------------------------------------------------|
 | 1.0.0   | 2026-Jan-20 | Initial release                                  |

## License

This project is licensed with the [MIT license](https://mit-license.org/).

## Contact
- [GitHub Repo](https://github.com/goodtocode/aspect-inboxoutbox)
- [@goodtocode](https://twitter.com/goodtocode)
