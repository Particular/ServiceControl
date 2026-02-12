namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
    public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
    {
        builder.ToTable("ProcessedMessages");
        builder.HasKey(e => new { e.Id, e.CreatedOn });
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CreatedOn).IsRequired();
        builder.Property(e => e.UniqueMessageId).HasMaxLength(200).IsRequired();

        // JSON columns
        builder.Property(e => e.HeadersJson).IsRequired();

        // Full-text search column (combines header values + body text)
        builder.Property(e => e.SearchableContent);

        // Denormalized query fields
        builder.Property(e => e.MessageId).HasMaxLength(200);
        builder.Property(e => e.MessageType).HasMaxLength(500);
        builder.Property(e => e.ConversationId).HasMaxLength(200);
        builder.Property(e => e.ReceivingEndpointName).HasMaxLength(500);
        builder.Property(e => e.BodyUrl).HasMaxLength(500);
        builder.Property(e => e.TimeSent);
        builder.Property(e => e.IsSystemMessage).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.BodySize).IsRequired();
        builder.Property(e => e.BodyNotStored).IsRequired();
        builder.Property(e => e.CriticalTimeTicks);
        builder.Property(e => e.ProcessingTimeTicks);
        builder.Property(e => e.DeliveryTimeTicks);

        // Uniqueness index (includes CreatedOn for partition alignment)
        builder.HasIndex(e => new { e.UniqueMessageId, e.CreatedOn });

        // QueryMessagesByConversationId
        builder.HasIndex(e => new { e.ConversationId, e.CreatedOn });

        // MessageId lookup (includes CreatedOn for partition alignment)
        builder.HasIndex(e => new { e.MessageId, e.CreatedOn });

        // TimeSent index for range queries
        builder.HasIndex(e => e.TimeSent);
    }
}
