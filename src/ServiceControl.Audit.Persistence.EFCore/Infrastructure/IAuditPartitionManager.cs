namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using ServiceControl.Audit.Persistence.EFCore.DbContexts;

interface IAuditPartitionManager
{
    Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, DateTime keepFrom, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every body stored for the rows that <see cref="DropExpired"/> removes for <paramref name="expired"/>.
    /// </summary>
    Task DeleteBodies(IAuditBodyStoragePersistence bodyStorage, DateTime expired, CancellationToken cancellationToken = default);

    Task<DropResult> DropExpired(AuditDbContext dbContext, DateTime start, int batchSize, CancellationToken cancellationToken = default);

    Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default);
}

readonly record struct DropResult(int RowsDeleted, bool Completed, bool Deferred = false);
