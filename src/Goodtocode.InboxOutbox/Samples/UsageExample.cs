using Goodtocode.InboxOutbox.Extensions;
using Goodtocode.InboxOutbox.Interceptors;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Goodtocode.InboxOutbox.Samples;

/// <summary>
/// Sample code demonstrating how to use the Inbox/Outbox infrastructure
/// </summary>
public class UsageExample
{
    /// <summary>
    /// Example: Configuring DbContext with Outbox Interceptor
    /// </summary>
    public static void ConfigureDbContext(IServiceCollection services)
    {
        services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
        {
            // Configure your database provider (e.g., UseSqlServer, UseNpgsql, etc.)
            // options.UseSqlServer("YourConnectionString");

            // Add the outbox interceptor
            var interceptor = serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);
        });
    }

    /// <summary>
    /// Example: Registering services in Program.cs
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        // Register inbox/outbox infrastructure
        services.AddInboxOutbox();

        // Register your event bus implementation
        services.AddSingleton<IEventBus, AzureServiceBusEventBus>();

        // Register your event consumer implementation
        services.AddSingleton<IEventConsumer, OrderEventConsumer>();
    }

    /// <summary>
    /// Example: Domain entity that raises events
    /// </summary>
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

    public enum OrderStatus
    {
        Pending,
        Processing,
        Completed
    }

    /// <summary>
    /// Example: Domain event
    /// </summary>
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

    /// <summary>
    /// Example: DbContext with Inbox/Outbox
    /// </summary>
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply inbox/outbox configurations
            modelBuilder.ApplyInboxOutbox();

            base.OnModelCreating(modelBuilder);
        }
    }

    /// <summary>
    /// Example: Event bus implementation (stub for Azure Service Bus)
    /// </summary>
    public class AzureServiceBusEventBus : IEventBus
    {
        public Task PublishAsync(object @event, CancellationToken cancellationToken = default)
        {
            // Implementation: Send to Azure Service Bus
            // var message = new ServiceBusMessage(JsonSerializer.Serialize(@event));
            // await sender.SendMessageAsync(message, cancellationToken);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Event consumer implementation
    /// </summary>
    public class OrderEventConsumer : IEventConsumer
    {
        public Task ConsumeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
        {
            // Implementation: Handle the consumed event
            // if (@event is OrderCompletedEvent orderCompleted)
            // {
            //     // Process the order completion
            // }
            return Task.CompletedTask;
        }
    }
}
