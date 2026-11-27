namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
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
        var entityType = entity switch
        {
            AuditRetentionEntity.AuditMessages => dbContext.Model.FindEntityType(typeof(AuditMessageEntity))!,
            AuditRetentionEntity.SagaSnapshots => dbContext.Model.FindEntityType(typeof(SagaSnapshotEntity))!,
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };
        var table = $"[{entityType.GetSchema()}].[{entityType.GetTableName()}]";

        // Raw SQL because ExecuteDeleteAsync cannot add the hints.
#pragma warning disable EF1002 // The table name comes from the EF Core model.
        var deleted = await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            DELETE FROM [a]
            FROM {table} AS [a]
            WHERE EXISTS (
                SELECT 1
                FROM (
                    SELECT TOP (@p0) [a0].[CreatedOn], [a0].[Id]
                    FROM {table} AS [a0] WITH (FORCESEEK)
                    WHERE [a0].[CreatedOn] = @p1
                    ORDER BY [a0].[Id]
                ) AS [a1]
                WHERE [a1].[CreatedOn] = [a].[CreatedOn] AND [a1].[Id] = [a].[Id]
            )
            OPTION (KEEPFIXED PLAN)
            """,
            [batchSize, hour],
            cancellationToken);
#pragma warning restore EF1002

        return new DropResult(deleted, Completed: deleted < batchSize);
    }

    public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTime?>(null);
}
