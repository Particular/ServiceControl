namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageGroupConfiguration : IEntityTypeConfiguration<FailedMessageGroupEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageGroupEntity> builder)
    {
        builder.HasKey(e => new { e.FailedMessageUniqueId, e.GroupId });

        builder.Property(e => e.GroupId).HasMaxLength(ColumnLengths.GroupIdLength).IsRequired();
        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(255).IsRequired();

        builder.HasIndex(e => e.GroupId);

        // Drives the per-classifier group aggregate, which filters on Type and groups by GroupId.
        // The Title INCLUDE column is added in the provider DbContexts (the IncludeProperties API
        // is provider-specific and is not available in this shared project).
        builder.HasIndex(e => new { e.Type, e.GroupId });

        builder.HasOne<FailedMessageEntity>()
            .WithMany()
            .HasForeignKey(e => e.FailedMessageUniqueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
