namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class ArchiveOperationConfiguration : IEntityTypeConfiguration<ArchiveOperationEntity>
{
    public void Configure(EntityTypeBuilder<ArchiveOperationEntity> builder)
    {
        builder.ToTable("ArchiveOperations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.RequestId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.GroupName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ArchiveType).IsRequired();
        builder.Property(e => e.ArchiveState).IsRequired();
        builder.Property(e => e.TotalNumberOfMessages).IsRequired();
        builder.Property(e => e.NumberOfMessagesArchived).IsRequired();
        builder.Property(e => e.NumberOfBatches).IsRequired();
        builder.Property(e => e.CurrentBatch).IsRequired();
        builder.Property(e => e.Started).IsRequired();
        builder.Property(e => e.Last);
        builder.Property(e => e.CompletionTime);

        builder.HasIndex(e => e.RequestId);
        builder.HasIndex(e => e.ArchiveState);
        builder.HasIndex(e => new { e.ArchiveType, e.RequestId }).IsUnique();
    }
}
