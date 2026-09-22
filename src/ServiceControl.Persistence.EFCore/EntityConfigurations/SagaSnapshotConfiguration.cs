namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class SagaSnapshotConfiguration : IEntityTypeConfiguration<SagaSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SagaSnapshotEntity> builder)
    {
        builder.HasKey(e => new { e.CreatedOn, e.Id });
        builder.Property(e => e.CreatedOn).ValueGeneratedNever();
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.SagaId).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.FinishTime).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.Property(e => e.SagaType).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.Endpoint).HasMaxLength(ColumnLengths.ShortTextLength);

        // Saga history is looked up by saga id and ordered by finish time.
        builder.HasIndex(e => new { e.SagaId, e.FinishTime });
    }
}
