namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageRetryConfiguration : IEntityTypeConfiguration<FailedMessageRetryEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageRetryEntity> builder)
    {
        builder.HasKey(e => e.UniqueMessageId);
        builder.Property(e => e.UniqueMessageId).ValueGeneratedNever();
        builder.Property(e => e.RetryId).HasMaxLength(ColumnLengths.ShortTextLength);
    }
}
