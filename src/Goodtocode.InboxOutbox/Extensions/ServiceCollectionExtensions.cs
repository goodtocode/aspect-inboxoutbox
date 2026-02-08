using Goodtocode.InboxOutbox.HostedServices;
using Goodtocode.InboxOutbox.Interceptors;
using Goodtocode.InboxOutbox.Interfaces;
using Goodtocode.InboxOutbox.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Goodtocode.InboxOutbox.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register inbox/outbox services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all required inbox/outbox services including interceptors and hosted services
    /// </summary>
    public static IServiceCollection AddInboxOutbox(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        // Register event type registry
        services.AddSingleton<IEventTypeRegistry, DefaultEventTypeRegistry>();

        // Register interceptor
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        // Register hosted services
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<InboxProcessorHostedService>();

        return services;
    }

    /// <summary>
    /// Registers inbox/outbox services with custom event type registry
    /// </summary>
    public static IServiceCollection AddInboxOutbox<TEventTypeRegistry>(
        this IServiceCollection services,
        IConfiguration? configuration = null)
        where TEventTypeRegistry : class, IEventTypeRegistry
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        // Register custom event type registry
        services.AddSingleton<IEventTypeRegistry, TEventTypeRegistry>();

        // Register interceptor
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        // Register hosted services
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<InboxProcessorHostedService>();

        return services;
    }
}
