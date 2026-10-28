namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

    /// <summary>
    /// Stands in for PostgreSQL when another session keeps a lock on an expired partition.
    /// </summary>
    sealed class BusyAuditPartitions : IAuditPartitionManager
    {
        static readonly DateTime ExpiredDay = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DateTime>>([ExpiredDay]);

        public Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime start, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DropResult(RowsDeleted: 0, Completed: true, Deferred: true));

        public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTime?>(null);
    }
}
