using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Goodtocode.InboxOutbox.HostedServices;

/// <summary>
/// Background service that processes and publishes outbox messages
/// </summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly TimeSpan _interval;

    public OutboxDispatcherHostedService(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = TimeSpan.FromMilliseconds(500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Dispatcher Hosted Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Outbox Dispatcher Hosted Service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetService<DbContext>();
        if (dbContext is null)
        {
            _logger.LogWarning("No DbContext registered. Outbox dispatcher cannot run.");
            return;
        }

        var eventBus = scope.ServiceProvider.GetService<IEventBus>();
        if (eventBus is null)
        {
            _logger.LogWarning("No IEventBus registered. Outbox dispatcher cannot run.");
            return;
        }

        var eventTypeRegistry = scope.ServiceProvider.GetService<IEventTypeRegistry>();
        if (eventTypeRegistry is null)
        {
            _logger.LogWarning("No IEventTypeRegistry registered. Outbox dispatcher cannot run.");
            return;
        }

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(x => x.Status == 0)
            .OrderBy(x => x.OccurredOnUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = eventTypeRegistry.Resolve(message.Type);
                var @event = JsonSerializer.Deserialize(message.Payload, eventType);

                if (@event is not null)
                {
                    await eventBus.PublishAsync(@event, cancellationToken);

                    message.Status = 1; // Published
                    message.LastDispatchedOnUtc = DateTime.UtcNow;
                    
                    _logger.LogInformation("Published outbox message {MessageId} of type {EventType}", 
                        message.Id, message.Type);
                }
            }
            catch (Exception ex)
            {
                message.Status = 2; // Failed
                message.LastDispatchError = ex.ToString();
                
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {EventType}", 
                    message.Id, message.Type);
            }
        }

        if (messages.Count != 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
