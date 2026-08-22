namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class AuditMessageConfiguration : IEntityTypeConfiguration<AuditMessageEntity>
{
    public void Configure(EntityTypeBuilder<AuditMessageEntity> builder)
    {
        builder.HasKey(e => new { e.CreatedOn, e.Id });
        builder.Property(e => e.CreatedOn).ValueGeneratedNever();
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.UniqueMessageId).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();
        builder.Property(e => e.IsSystemMessage).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.HeadersJson).IsRequired();
        builder.Property(e => e.BodyStoredExternally).IsRequired();
        builder.Property(e => e.BodySize).IsRequired();

        builder.Property(e => e.MessageId).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ConversationId).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.SendingEndpointName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.SendingEndpointHost).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ReceivingEndpointName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.ReceivingEndpointHost).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.BodyContentType).HasMaxLength(ColumnLengths.ShortTextLength);

        // Resolves an audit row from a failed message's id, and is what the third step of the body
        // arbitration order looks up.
        builder.HasIndex(e => e.UniqueMessageId);

        // Serves both the per-endpoint message queries and the daily audit counts.
        builder.HasIndex(e => new { e.ReceivingEndpointName, e.CreatedOn });

        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.ProcessedAt);
        builder.HasIndex(e => e.TimeSent);
    }
}
