namespace ServiceControl.Persistence.EFCore.SqlServer;

using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class SqlServerServiceControlDbContext(DbContextOptions<SqlServerServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Drives the retention sweep. The index is restricted to the statuses the sweep deletes
        // (Resolved and Archived) by a provider specific filter, applied in the provider DbContext.
        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => e.StatusChangedAt)
            .HasFilter($"[Status] IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");

        // Widen the group-aggregate indexes with covering INCLUDE columns so the
        // /api/recoverability/groups/ aggregate is index-only (no key lookups / clustered scan with a
        // residual Status predicate). The IncludeProperties API is
        // provider-specific, so the widening is applied here rather than in the shared configuration.
        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => new { e.Status, e.LastModified })
            .IncludeProperties(nameof(FailedMessageEntity.FirstTimeOfFailure), nameof(FailedMessageEntity.LastTimeOfFailure));

        modelBuilder.Entity<FailedMessageGroupEntity>()
            .HasIndex(e => new { e.Type, e.GroupId })
            .IncludeProperties(nameof(FailedMessageGroupEntity.Title));
    }

    public override bool IsDuplicateKeyException(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }
}
