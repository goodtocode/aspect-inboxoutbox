using Goodtocode.InboxOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Goodtocode.InboxOutbox.Configurations;

/// <summary>
/// Entity Framework configuration for InboxMessage entity
/// </summary>
public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.ReceivedOnUtc)
            .IsRequired();
        
        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.Payload)
            .IsRequired();
        
        builder.Property(x => x.Status)
            .IsRequired();
        
        builder.Property(x => x.ProcessedOnUtc)
            .IsRequired(false);
        
        builder.Property(x => x.ProcessingError)
            .IsRequired(false);
        
        builder.HasIndex(x => new { x.Status, x.ReceivedOnUtc });
    }
}
