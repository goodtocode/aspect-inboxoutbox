using Goodtocode.InboxOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Goodtocode.InboxOutbox.Configurations;

/// <summary>
/// Entity Framework configuration for OutboxMessage entity
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();
        
        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.Payload)
            .IsRequired();
        
        builder.Property(x => x.Status)
            .IsRequired();
        
        builder.Property(x => x.LastDispatchedOnUtc)
            .IsRequired(false);
        
        builder.Property(x => x.LastDispatchError)
            .IsRequired(false);
        
        builder.HasIndex(x => new { x.Status, x.OccurredOnUtc });
    }
}
