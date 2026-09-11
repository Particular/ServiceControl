namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessageEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageEntity> builder)
    {
        builder.HasKey(e => e.UniqueMessageId);
        builder.Property(e => e.UniqueMessageId).ValueGeneratedNever();

        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.StatusChangedAt).IsRequired();
        builder.Property(e => e.LastModified).IsRequired();
        builder.Property(e => e.NumberOfProcessingAttempts).IsRequired();
        builder.Property(e => e.FirstTimeOfFailure).IsRequired();
        builder.Property(e => e.LastTimeOfFailure).IsRequired();
        builder.Property(e => e.LastAttemptedAt).IsRequired();

        builder.Property(e => e.MessageId).HasMaxLength(ColumnLengths.ShortTextLength);
        // 450 and not nvarchar(max): the column has to be indexable to serve sort=message_type, and
        // SQL Server rejects nvarchar(max) as an index key column. Type names are short-by-nature
        // (the enricher stores the first comma token of EnclosedMessageTypes); ingestion enforces
        // the cap so the write path can never fail on a longer value.
        builder.Property(e => e.MessageType).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ConversationId).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.SendingEndpointName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.SendingEndpointHost).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ReceivingEndpointName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ReceivingEndpointHost).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.FailingEndpointAddress).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.BodyContentType).HasMaxLength(ColumnLengths.ShortTextLength);

        builder.Property(e => e.IsSystemMessage).IsRequired();
        builder.Property(e => e.HeadersJson).IsRequired();
        builder.Property(e => e.BodyStoredExternally).IsRequired();
        builder.Property(e => e.BodySize).IsRequired();

        // Drives the group aggregate's MIN/MAX(FirstTimeOfFailure, LastTimeOfFailure) over the
        // unresolved set. The FirstTimeOfFailure/LastTimeOfFailure INCLUDE columns are added in the
        // provider DbContexts (the IncludeProperties API is provider-specific and is not available in
        // this shared project).
        builder.HasIndex(e => new { e.Status, e.LastModified });

        // Serves the failed-messages page sorted by time_of_failure (ServicePulse default sort).
        // Keyed (Status, LastTimeOfFailure) so the page query streams instead of scanning the
        // clustered table.
        builder.HasIndex(e => new { e.Status, e.LastTimeOfFailure });

        // Serves the failed-messages page sorted by message_type. UniqueMessageId is an explicit
        // key column and not just the SQL Server row locator: a message type repeats across many
        // failed messages, and the page query's tie-break (ORDER BY MessageType DESC,
        // UniqueMessageId DESC) has to come from the index itself on providers without a row
        // locator concept (PostgreSQL), or the sort degrades to sorting every tie group.
        builder.HasIndex(e => new { e.Status, e.MessageType, e.UniqueMessageId });
        builder.HasIndex(e => e.ReceivingEndpointName);
        builder.HasIndex(e => e.FailingEndpointAddress);
        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.TimeSent);

        // Drives the retention sweep. The index is restricted to the statuses the sweep deletes
        // (Resolved and Archived) by a provider specific filter, applied in the provider DbContext.
        builder.HasIndex(e => e.StatusChangedAt);
    }
}
