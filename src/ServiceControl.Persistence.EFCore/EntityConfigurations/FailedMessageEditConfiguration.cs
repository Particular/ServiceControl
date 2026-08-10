namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageEditConfiguration : IEntityTypeConfiguration<FailedMessageEditEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageEditEntity> builder)
    {
        builder.HasKey(e => e.UniqueMessageId);
        builder.Property(e => e.UniqueMessageId).ValueGeneratedNever();

        // NServiceBus message ids are stringified GUIDs; ShortTextLength (450) is consistent with
        // other message-id columns and lets the column be indexed.
        builder.Property(e => e.EditId)
            .IsRequired()
            .HasMaxLength(ColumnLengths.ShortTextLength);

        builder.HasIndex(e => e.EditId);
    }
}