using Goodtocode.InboxOutbox.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Goodtocode.InboxOutbox.Extensions;

/// <summary>
/// Extension methods for ModelBuilder to apply inbox/outbox configurations
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies inbox and outbox entity configurations to the DbContext
    /// </summary>
    public static ModelBuilder ApplyInboxOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        return modelBuilder;
    }
}
