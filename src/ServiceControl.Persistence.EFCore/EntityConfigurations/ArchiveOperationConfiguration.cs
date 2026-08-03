namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ArchiveOperationConfiguration : IEntityTypeConfiguration<ArchiveOperationEntity>
{
    public void Configure(EntityTypeBuilder<ArchiveOperationEntity> builder)
    {
        builder.ToTable("ArchiveOperations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(200)
            .ValueGeneratedNever();

        builder.Property(e => e.RequestId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.GroupName).IsRequired();
        builder.Property(e => e.ArchiveType).IsRequired();
        builder.Property(e => e.IsArchive).IsRequired();
        builder.Property(e => e.TotalNumberOfMessages).IsRequired();
        builder.Property(e => e.NumberOfMessagesProcessed).IsRequired();
        builder.Property(e => e.NumberOfBatches).IsRequired();
        builder.Property(e => e.CurrentBatch).IsRequired();
        builder.Property(e => e.Started).IsRequired();

        builder.Property(e => e.InitiatedById).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.InitiatedByName).HasMaxLength(ColumnLengths.ShortTextLength);
        builder.Property(e => e.OperationId).HasMaxLength(ColumnLengths.ShortTextLength);

        // Enforce one operation per (RequestId, ArchiveType, IsArchive) at a time
        builder.HasIndex(e => new { e.RequestId, e.ArchiveType, e.IsArchive })
            .IsUnique();

        // Non-unique index for cleanup / diagnostics queries
        builder.HasIndex(e => e.Started);
    }
}