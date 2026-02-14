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
public sealed class InboxProcessorHostedService(
    IServiceProvider serviceProvider,
    ILogger<InboxProcessorHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<InboxProcessorHostedService> _logger = logger;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInboxProcessorStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogErrorProcessingInboxMessages(ex);
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInboxProcessorStopped();
    }

    private async Task ProcessInboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetService<DbContext>();
        if (dbContext is null)
        {
            _logger.LogNoDbContextRegisteredInbox();
            return;
        }

        var eventConsumer = scope.ServiceProvider.GetService<IEventConsumer>();
        if (eventConsumer is null)
        {
            _logger.LogNoEventConsumerRegistered();
            return;
        }

        var eventTypeRegistry = scope.ServiceProvider.GetService<IEventTypeRegistry>();
        if (eventTypeRegistry is null)
        {
            _logger.LogNoEventTypeRegistryRegisteredInbox();
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

                    _logger.LogProcessedInboxMessage(message.Id, message.Type);
                }
            }
            catch (Exception ex)
            {
                message.Status = 2; // Failed
                message.ProcessingError = ex.ToString();

                _logger.LogFailedToProcessInboxMessage(ex, message.Id, message.Type);
            }
        }

        if (messages.Count != 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public static partial class InboxLoggerExtensions
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Inbox Processor Hosted Service started")]
    public static partial void LogInboxProcessorStarted(this ILogger logger);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Inbox Processor Hosted Service stopped")]
    public static partial void LogInboxProcessorStopped(this ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Error processing inbox messages")]
    public static partial void LogErrorProcessingInboxMessages(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "No DbContext registered. Inbox processor cannot run.")]
    public static partial void LogNoDbContextRegisteredInbox(this ILogger logger);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning, Message = "No IEventConsumer registered. Inbox processor cannot run.")]
    public static partial void LogNoEventConsumerRegistered(this ILogger logger);

    [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "No IEventTypeRegistry registered. Inbox processor cannot run.")]
    public static partial void LogNoEventTypeRegistryRegisteredInbox(this ILogger logger);

    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "Processed inbox message {MessageId} of type {EventType}")]
    public static partial void LogProcessedInboxMessage(this ILogger logger, Guid messageId, string eventType);

    [LoggerMessage(EventId = 17, Level = LogLevel.Error, Message = "Failed to process inbox message {MessageId} of type {EventType}")]
    public static partial void LogFailedToProcessInboxMessage(this ILogger logger, Exception exception, Guid messageId, string eventType);
}
