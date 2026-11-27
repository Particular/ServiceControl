namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

// No partitions, because a full text index cannot be aligned to a partition scheme.
class SqlServerAuditPartitionManager : IAuditPartitionManager
{
    public Task EnsurePartitions(AuditDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default)
    {
        // One seek on the clustered key for each expired hour.
        var expired = new List<DateTime>();
        DateTime? last = null;

        while (true)
        {
            var after = last;
            var hours = entity switch
            {
                AuditRetentionEntity.AuditMessages => dbContext.AuditMessages.Where(message => message.CreatedOn < keepFrom && (after == null || message.CreatedOn > after)).OrderBy(message => message.CreatedOn).Select(message => (DateTime?)message.CreatedOn),
                AuditRetentionEntity.SagaSnapshots => dbContext.SagaSnapshots.Where(snapshot => snapshot.CreatedOn < keepFrom && (after == null || snapshot.CreatedOn > after)).OrderBy(snapshot => snapshot.CreatedOn).Select(snapshot => (DateTime?)snapshot.CreatedOn),
                _ => throw new ArgumentOutOfRangeException(nameof(entity))
            };

            var next = await hours.FirstOrDefaultAsync(cancellationToken);
            if (next is null)
            {
                return expired;
            }

            expired.Add(next.Value);
            last = next;
        }
    }

    public Task DeleteBodies(IAuditBodyStoragePersistence bodyStorage, DateTime hour, CancellationToken cancellationToken = default) =>
        bodyStorage.DeleteBodiesForHour(hour, cancellationToken);

    public async Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime hour, int batchSize, CancellationToken cancellationToken = default)
    {
        var deleted = entity switch
        {
            AuditRetentionEntity.AuditMessages => await dbContext.AuditMessages
                .Where(message => message.CreatedOn == hour)
                .OrderBy(message => message.Id)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken),
            AuditRetentionEntity.SagaSnapshots => await dbContext.SagaSnapshots
                .Where(snapshot => snapshot.CreatedOn == hour)
                .OrderBy(snapshot => snapshot.Id)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

        return new DropResult(deleted, Completed: deleted < batchSize);
    }

    public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTime?>(null);
}
