namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessageEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageEntity> builder)
    {
        builder.ToTable("FailedMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.UniqueMessageId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ProcessingAttemptsJson).IsRequired();
        builder.Property(e => e.FailureGroupsJson).IsRequired();

        // Denormalized query fields from FailureGroups
        builder.Property(e => e.PrimaryFailureGroupId).HasMaxLength(200);

        // Denormalized query fields from processing attempts
        builder.Property(e => e.MessageId).HasMaxLength(200);
        builder.Property(e => e.MessageType).HasMaxLength(500);
        builder.Property(e => e.SendingEndpointName).HasMaxLength(500);
        builder.Property(e => e.ReceivingEndpointName).HasMaxLength(500);
        builder.Property(e => e.ExceptionType).HasMaxLength(500);
        builder.Property(e => e.QueueAddress).HasMaxLength(500);
        builder.Property(e => e.ConversationId).HasMaxLength(200);

        // PRIMARY: Critical for uniqueness and upserts
        builder.HasIndex(e => e.UniqueMessageId).IsUnique();

        // COMPOSITE INDEXES: Hot paths - Status is involved in most queries
        // Most common pattern: Status + LastProcessedAt (15+ queries)
        builder.HasIndex(e => new { e.Status, e.LastProcessedAt });

        // Endpoint-specific queries (8+ queries)
        builder.HasIndex(e => new { e.ReceivingEndpointName, e.Status, e.LastProcessedAt });

        // Queue-specific retry operations (6+ queries)
        builder.HasIndex(e => new { e.QueueAddress, e.Status, e.LastProcessedAt });

        // Retry operations by queue (3+ queries)
        builder.HasIndex(e => new { e.Status, e.QueueAddress });

        // TIME-BASED QUERIES
        // Endpoint + time range queries (for GetAllMessagesForEndpoint)
        builder.HasIndex(e => new { e.ReceivingEndpointName, e.TimeSent });

        // Conversation tracking queries
        builder.HasIndex(e => new { e.ConversationId, e.LastProcessedAt });

        // SEARCH QUERIES
        // Message type + time filtering
        builder.HasIndex(e => new { e.MessageType, e.TimeSent });

        // FAILURE GROUP QUERIES
        // Critical for group-based filtering (avoids loading all messages)
        builder.HasIndex(e => new { e.PrimaryFailureGroupId, e.Status, e.LastProcessedAt });

        // SINGLE-COLUMN INDEXES: Keep for specific lookup cases
        builder.HasIndex(e => e.MessageId);

        // PERFORMANCE METRICS INDEXES: For sorting operations
        builder.HasIndex(e => e.CriticalTime);
        builder.HasIndex(e => e.ProcessingTime);
        builder.HasIndex(e => e.DeliveryTime);
    }
}
