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
    /// Fails to list the expired rows of one kind of row, and hands everything else to the real partition manager.
    /// </summary>
    sealed class FailingAuditPartitions(AuditRetentionEntity failing, IAuditPartitionManager inner) : IAuditPartitionManager
    {
        public const string Reason = "The table is unreachable";

        public Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) =>
            inner.EnsurePartitions(dbContext, from, toExclusive, cancellationToken);

        public Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default) =>
            entity == failing
                ? Task.FromException<IReadOnlyList<DateTime>>(new InvalidOperationException(Reason))
                : inner.ListExpired(dbContext, entity, keepFrom, cancellationToken);

        public Task DeleteBodies(IAuditBodyStoragePersistence bodyStorage, DateTime expired, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime start, int batchSize, CancellationToken cancellationToken = default) =>
            inner.DropExpired(dbContext, entity, start, batchSize, cancellationToken);

        public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
            inner.ProvisionedUntil(dbContext, cancellationToken);
    }
}
