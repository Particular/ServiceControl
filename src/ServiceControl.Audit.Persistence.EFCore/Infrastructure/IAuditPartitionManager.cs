namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

interface IAuditPartitionManager
{
    Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default);

    Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime start, int batchSize, CancellationToken cancellationToken = default);

    Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default);
}

readonly record struct DropResult(int RowsDeleted, bool Completed, bool Deferred = false);
