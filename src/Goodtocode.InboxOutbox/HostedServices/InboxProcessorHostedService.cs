using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Goodtocode.InboxOutbox.HostedServices;

/// <summary>
/// Background service that processes inbox messages
/// </summary>
public sealed class InboxProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxProcessorHostedService> _logger;
    private readonly TimeSpan _interval;

    public InboxProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<InboxProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = TimeSpan.FromMilliseconds(500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inbox Processor Hosted Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox messages");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Inbox Processor Hosted Service stopped");
    }

    private async Task ProcessInboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetService<DbContext>();
        if (dbContext is null)
        {
            _logger.LogWarning("No DbContext registered. Inbox processor cannot run.");
            return;
        }

        var eventConsumer = scope.ServiceProvider.GetService<IEventConsumer>();
        if (eventConsumer is null)
        {
            _logger.LogWarning("No IEventConsumer registered. Inbox processor cannot run.");
            return;
        }

        var eventTypeRegistry = scope.ServiceProvider.GetService<IEventTypeRegistry>();
        if (eventTypeRegistry is null)
        {
            _logger.LogWarning("No IEventTypeRegistry registered. Inbox processor cannot run.");
            return;
        }

        var messages = await dbContext.Set<InboxMessage>()
            .Where(x => x.Status == 0)
            .OrderBy(x => x.ReceivedOnUtc)
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
                    await eventConsumer.ConsumeAsync(@event, cancellationToken);

                    message.Status = 1; // Processed
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    
                    _logger.LogInformation("Processed inbox message {MessageId} of type {EventType}", 
                        message.Id, message.Type);
                }
            }
            catch (Exception ex)
            {
                message.Status = 2; // Failed
                message.ProcessingError = ex.ToString();
                
                _logger.LogError(ex, "Failed to process inbox message {MessageId} of type {EventType}", 
                    message.Id, message.Type);
            }
        }

        if (messages.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
