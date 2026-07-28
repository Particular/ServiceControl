namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageGroupConfiguration : IEntityTypeConfiguration<FailedMessageGroupEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageGroupEntity> builder)
    {
        builder.HasKey(e => new { e.FailedMessageUniqueId, e.GroupId });

        // Group ids are deterministic Guid strings
        builder.Property(e => e.GroupId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(255).IsRequired();

        builder.HasIndex(e => e.GroupId);

        builder.HasOne<FailedMessageEntity>()
            .WithMany()
            .HasForeignKey(e => e.FailedMessageUniqueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
