namespace ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

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

        builder.HasIndex(e => new { e.SagaId, e.FinishTime });
    }
}
