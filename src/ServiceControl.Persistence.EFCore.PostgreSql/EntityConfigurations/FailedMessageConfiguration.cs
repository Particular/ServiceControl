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
            .HasFilter($"status IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");

        builder.HasIndex(e => new { e.Status, e.LastModified, e.UniqueMessageId })
            .IncludeProperties(nameof(FailedMessageEntity.FirstTimeOfFailure), nameof(FailedMessageEntity.LastTimeOfFailure));
    }
}