namespace ServiceControl.Persistence.EFCore.SqlServer.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// SQL Server does not partition the audit tables: a full text index cannot be aligned to a
// partition scheme, so retention deletes by hour instead. There is nothing to provision.
class SqlServerAuditPartitionManager : IAuditPartitionManager
{
    public Task EnsurePartitions(ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
