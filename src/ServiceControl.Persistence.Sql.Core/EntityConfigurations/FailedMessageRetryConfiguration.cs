namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageRetryConfiguration : IEntityTypeConfiguration<FailedMessageRetryEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageRetryEntity> builder)
    {
        builder.ToTable("FailedMessageRetries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.FailedMessageId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RetryBatchId).HasMaxLength(200);
        builder.Property(e => e.StageAttempts).IsRequired();

        // Indexes
        builder.HasIndex(e => e.FailedMessageId);
        builder.HasIndex(e => e.RetryBatchId);
    }
}
