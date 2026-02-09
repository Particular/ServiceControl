namespace ServiceControl.Audit.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class SagaSnapshotConfiguration : IEntityTypeConfiguration<SagaSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SagaSnapshotEntity> builder)
    {
        builder.ToTable("SagaSnapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.SagaId).IsRequired();
        builder.Property(e => e.SagaType).IsRequired();
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.FinishTime);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.StateAfterChange).IsRequired();
        builder.Property(e => e.InitiatingMessageJson).IsRequired();
        builder.Property(e => e.OutgoingMessagesJson).IsRequired();
        builder.Property(e => e.Endpoint).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.HasIndex(e => e.SagaId);
        builder.HasIndex(e => e.ProcessedAt);

        // Batch retention cleanup index
        builder.Property(e => e.BatchId).IsRequired();
        builder.HasIndex(e => new { e.BatchId, e.ProcessedAt })
            .HasDatabaseName("IX_SagaSnapshots_BatchId_ProcessedAt");
    }
}
