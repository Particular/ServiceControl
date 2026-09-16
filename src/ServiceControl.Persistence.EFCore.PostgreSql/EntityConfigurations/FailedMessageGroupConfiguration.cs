namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageGroupConfiguration : IEntityTypeConfiguration<FailedMessageGroupEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageGroupEntity> builder)
    {
        // Both the join column (FailedMessageUniqueId) and the per-group title lookup's column
        // (Title) have to be INCLUDEs. Without Title the lookup heap-fetches, and without
        // FailedMessageUniqueId step 1 of the aggregate cannot join index-only
        builder.HasIndex(e => new { e.Type, e.GroupId })
            .IncludeProperties(nameof(FailedMessageGroupEntity.FailedMessageUniqueId), nameof(FailedMessageGroupEntity.Title));
    }
}