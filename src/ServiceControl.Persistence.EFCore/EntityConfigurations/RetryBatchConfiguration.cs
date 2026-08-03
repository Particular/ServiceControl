namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class RetryBatchConfiguration : IEntityTypeConfiguration<RetryBatchEntity>
{
    public void Configure(EntityTypeBuilder<RetryBatchEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.RetryType).IsRequired();
        builder.Property(e => e.InitialBatchSize).IsRequired();
        builder.Property(e => e.StartTime).IsRequired();

        builder.Property(e => e.RetrySessionId).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.RequestId).IsRequired().HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.StagingId).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.OperationId).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.InitiatedById).HasMaxLength(ColumnLengths.ShortTextLength);

        builder.HasIndex(e => new { e.Status, e.RetrySessionId });
    }
}
