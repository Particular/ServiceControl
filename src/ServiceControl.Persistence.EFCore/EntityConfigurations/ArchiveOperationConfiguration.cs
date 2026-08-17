namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ArchiveOperationConfiguration : IEntityTypeConfiguration<ArchiveOperationEntity>
{
    public void Configure(EntityTypeBuilder<ArchiveOperationEntity> builder)
    {
        // Composite primary key — the natural key that distinguishes one operation from another.
        // This also serves as the uniqueness constraint: one operation per (RequestId, ArchiveType, OperationType).
        builder.HasKey(e => new { e.RequestId, e.ArchiveType, e.OperationType });

        builder.Property(e => e.RequestId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.GroupName).IsRequired();
        builder.Property(e => e.ArchiveType).IsRequired();
        builder.Property(e => e.OperationType).IsRequired();
        builder.Property(e => e.TotalNumberOfMessages).IsRequired();
        builder.Property(e => e.NumberOfMessagesProcessed).IsRequired();
        builder.Property(e => e.NumberOfBatches).IsRequired();
        builder.Property(e => e.CurrentBatch).IsRequired();
        builder.Property(e => e.Started).IsRequired();

        builder.Property(e => e.InitiatedById).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.InitiatedByName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.OperationId).HasMaxLength(ColumnLengths.ShortTextLength);

        // Non-unique index for cleanup / diagnostics queries
        builder.HasIndex(e => e.Started);
    }
}