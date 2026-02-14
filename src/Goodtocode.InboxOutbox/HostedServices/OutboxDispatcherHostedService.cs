using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Extensions;
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
public sealed class OutboxDispatcherHostedService(
    IServiceProvider serviceProvider,
    ILogger<OutboxDispatcherHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OutboxDispatcherHostedService> _logger = logger;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogOutboxDispatcherStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogErrorProcessingOutboxMessages(ex);
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogOutboxDispatcherStopped();
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetService<DbContext>();
        if (dbContext is null)
        {
            _logger.LogNoDbContextRegistered();
            return;
        }

        var eventBus = scope.ServiceProvider.GetService<IEventBus>();
        if (eventBus is null)
        {
            _logger.LogNoEventBusRegistered();
            return;
        }

        var eventTypeRegistry = scope.ServiceProvider.GetService<IEventTypeRegistry>();
        if (eventTypeRegistry is null)
        {
            _logger.LogNoEventTypeRegistryRegistered();
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

                    _logger.LogPublishedOutboxMessage(message.Id, message.Type);
                }
            }
            catch (Exception ex)
            {
                message.Status = 2; // Failed
                message.LastDispatchError = ex.ToString();
                _logger.LogFailedToPublishOutboxMessage(ex, message.Id, message.Type);
            }
        }

        if (messages.Count != 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public static partial class LoggerExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Outbox Dispatcher Hosted Service started")]
    public static partial void LogOutboxDispatcherStarted(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Outbox Dispatcher Hosted Service stopped")]
    public static partial void LogOutboxDispatcherStopped(this ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error processing outbox messages")]
    public static partial void LogErrorProcessingOutboxMessages(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "No DbContext registered. Outbox dispatcher cannot run.")]
    public static partial void LogNoDbContextRegistered(this ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "No IEventBus registered. Outbox dispatcher cannot run.")]
    public static partial void LogNoEventBusRegistered(this ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "No IEventTypeRegistry registered. Outbox dispatcher cannot run.")]
    public static partial void LogNoEventTypeRegistryRegistered(this ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Published outbox message {MessageId} of type {EventType}")]
    public static partial void LogPublishedOutboxMessage(this ILogger logger, Guid messageId, string eventType);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Failed to publish outbox message {MessageId} of type {EventType}")]
    public static partial void LogFailedToPublishOutboxMessage(this ILogger logger, Exception exception, Guid messageId, string eventType);
}
