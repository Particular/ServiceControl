namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

// No partitions, because a full text index cannot be aligned to a partition scheme.
class SqlServerAuditPartitionManager : IAuditPartitionManager
{
    public Task EnsurePartitions(AuditDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, DateTime keepFrom, CancellationToken cancellationToken = default)
    {
        var messageHours = dbContext.AuditMessages.Where(message => message.CreatedOn < keepFrom).Select(message => message.CreatedOn);
        var snapshotHours = dbContext.SagaSnapshots.Where(snapshot => snapshot.CreatedOn < keepFrom).Select(snapshot => snapshot.CreatedOn);

        return await messageHours.Union(snapshotHours).OrderBy(hour => hour).ToListAsync(cancellationToken);
    }

    public async Task<DropResult> DropExpired(AuditDbContext dbContext, DateTime hour, int batchSize, CancellationToken cancellationToken = default)
    {
        var messagesDeleted = await dbContext.AuditMessages
            .Where(message => message.CreatedOn == hour)
            .OrderBy(message => message.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        if (messagesDeleted == batchSize)
        {
            return new DropResult(messagesDeleted, Completed: false);
        }

        var snapshotsDeleted = await dbContext.SagaSnapshots
            .Where(snapshot => snapshot.CreatedOn == hour)
            .OrderBy(snapshot => snapshot.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        return new DropResult(messagesDeleted + snapshotsDeleted, Completed: snapshotsDeleted < batchSize);
    }

    public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTime?>(null);
}
