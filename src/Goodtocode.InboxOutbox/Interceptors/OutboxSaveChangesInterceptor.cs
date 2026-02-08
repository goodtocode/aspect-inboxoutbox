using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Goodtocode.InboxOutbox.Interceptors;

/// <summary>
/// EF Core interceptor that automatically captures domain events and writes them to the outbox
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = eventData.Context;
        var domainEntities = context.ChangeTracker
            .Entries<IDomainEntity>()
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
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    Status = 0
                };

                context.Add(outbox);
            }

            entry.Entity.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

/// <summary>
/// Interface for entities that can raise domain events
/// </summary>
public interface IDomainEntity
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
