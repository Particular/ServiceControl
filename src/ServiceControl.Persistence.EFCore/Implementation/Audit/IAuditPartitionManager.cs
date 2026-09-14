namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// The provider-specific lifecycle of the audit tables' hourly partitions. Only the owner of a
/// database calls this: setup, to provision the initial window, and the retention sweeper, to keep
/// the window ahead of the clock. Ingestion never issues DDL.
/// </summary>
public interface IAuditPartitionManager
{
    /// <summary>
    /// Creates every hourly partition in [fromHour, toHourExclusive) that does not exist yet, for
    /// both audit tables. A no-op on a provider that does not partition.
    /// </summary>
    Task EnsurePartitions(ServiceControlDbContext dbContext, DateTime fromHour, DateTime toHourExclusive, CancellationToken cancellationToken = default);
}
