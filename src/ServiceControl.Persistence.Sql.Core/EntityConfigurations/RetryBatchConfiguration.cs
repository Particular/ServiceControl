namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class RetryBatchConfiguration : IEntityTypeConfiguration<RetryBatchEntity>
{
    public void Configure(EntityTypeBuilder<RetryBatchEntity> builder)
    {
        builder.ToTable("RetryBatches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.RetrySessionId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RequestId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.StagingId).HasMaxLength(200);
        builder.Property(e => e.Originator).HasMaxLength(500);
        builder.Property(e => e.Classifier).HasMaxLength(500);
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.RetryType).IsRequired();
        builder.Property(e => e.FailureRetriesJson).IsRequired();

        // Indexes
        builder.HasIndex(e => e.RetrySessionId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.StagingId);
    }
}
