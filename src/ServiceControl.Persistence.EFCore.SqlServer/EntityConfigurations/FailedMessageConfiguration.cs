namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using MessageFailures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessageEntity>
{
    public void Configure(EntityTypeBuilder<FailedMessageEntity> builder)
    {
        // Drives the retention sweep.
        // The index is restricted to the statuses the sweep deletes (Resolved and Archived)
        builder.HasIndex(e => e.StatusChangedAt)
            .HasFilter($"[Status] IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");

        // Widen the group-aggregate indexes with covering INCLUDE columns so the
        // /api/recoverability/groups/ aggregate is index-only (no key lookups / clustered scan with a
        // residual Status predicate).
        builder.HasIndex(e => new { e.Status, e.LastModified })
            .IncludeProperties(nameof(FailedMessageEntity.FirstTimeOfFailure), nameof(FailedMessageEntity.LastTimeOfFailure));
    }
}
