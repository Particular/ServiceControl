namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
    public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
    {
        builder.ToTable("ProcessedMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.UniqueMessageId).HasMaxLength(200).IsRequired();

        // JSON columns
        builder.Property(e => e.MessageMetadataJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.HeadersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.InvokedSagasJson).HasColumnType("jsonb");
        builder.Property(e => e.OriginatesFromSagaJson).HasColumnType("jsonb");

        // Full-text search column
        builder.Property(e => e.Body); // Will be mapped to text/nvarchar(max) per database

        // Denormalized query fields
        builder.Property(e => e.MessageId).HasMaxLength(200);
        builder.Property(e => e.MessageType).HasMaxLength(500);
        builder.Property(e => e.ConversationId).HasMaxLength(200);
        builder.Property(e => e.SendingEndpointName).HasMaxLength(500);
        builder.Property(e => e.ReceivingEndpointName).HasMaxLength(500);
        builder.Property(e => e.BodyUrl).HasMaxLength(500);

        // PRIMARY: Uniqueness index
        builder.HasIndex(e => e.UniqueMessageId);

        // COMPOSITE INDEXES: Based on IAuditDataStore query patterns

        // GetMessages: includeSystemMessages, timeSent range, sort by ProcessedAt/TimeSent
        builder.HasIndex(e => new { e.IsSystemMessage, e.TimeSent, e.ProcessedAt });

        // QueryMessagesByReceivingEndpoint: endpoint + system messages + time range
        builder.HasIndex(e => new { e.ReceivingEndpointName, e.IsSystemMessage, e.TimeSent, e.ProcessedAt });

        // QueryMessagesByConversationId
        builder.HasIndex(e => new { e.ConversationId, e.ProcessedAt });

        // QueryAuditCounts: endpoint + system + processed at (date grouping)
        builder.HasIndex(e => new { e.ReceivingEndpointName, e.IsSystemMessage, e.ProcessedAt });

        // Retention/cleanup by expiration
        builder.HasIndex(e => e.ExpiresAt);

        // MessageId lookup (for body retrieval)
        builder.HasIndex(e => e.MessageId);
    }
}
