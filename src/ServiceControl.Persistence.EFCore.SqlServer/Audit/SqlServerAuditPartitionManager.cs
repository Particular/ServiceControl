namespace ServiceControl.Persistence.EFCore.SqlServer.Audit;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// SQL Server does not partition the audit tables: a full text index cannot be aligned to a
// partition scheme, so an expired hour is deleted in bounded batches instead of dropped.
class SqlServerAuditPartitionManager : IAuditPartitionManager
{
    public Task EnsurePartitions(ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<IReadOnlyList<DateTime>> ListExpiredHours(ServiceControlDbContext dbContext, DateTime lastExpiredHour, CancellationToken cancellationToken = default)
    {
        var messageHours = dbContext.AuditMessages.Where(m => m.CreatedOn <= lastExpiredHour).Select(m => m.CreatedOn);
        var snapshotHours = dbContext.SagaSnapshots.Where(s => s.CreatedOn <= lastExpiredHour).Select(s => s.CreatedOn);

        return await messageHours.Union(snapshotHours).OrderBy(hour => hour).ToListAsync(cancellationToken);
    }

    public async Task<HourDrop> DropHour(ServiceControlDbContext dbContext, DateTime hour, int batchSize, CancellationToken cancellationToken = default)
    {
        var messagesDeleted = await dbContext.AuditMessages
            .Where(m => m.CreatedOn == hour)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        if (messagesDeleted == batchSize)
        {
            return new HourDrop(messagesDeleted, Completed: false);
        }

        var snapshotsDeleted = await dbContext.SagaSnapshots
            .Where(s => s.CreatedOn == hour)
            .OrderBy(s => s.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        return new HourDrop(messagesDeleted + snapshotsDeleted, Completed: snapshotsDeleted < batchSize);
    }

    public Task<DateTime?> NewestProvisionedHourEnd(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTime?>(null);
}
