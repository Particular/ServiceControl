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

    /// <summary>
    /// The hours at or before <paramref name="lastExpiredHour"/> that still hold rows, or a
    /// partition, in either audit table. Oldest first.
    /// </summary>
    Task<IReadOnlyList<DateTime>> ListExpiredHours(ServiceControlDbContext dbContext, DateTime lastExpiredHour, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the hour from both audit tables. A partitioning provider drops the hour's partitions
    /// in one call. A deleting provider removes at most <paramref name="batchSize"/> rows per table
    /// per call and reports whether anything is left, so the caller can pace the batches.
    /// </summary>
    Task<HourDrop> DropHour(ServiceControlDbContext dbContext, DateTime hour, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the newest provisioned partition ends, or null where the provider does not partition
    /// and so can never run out of provisioned hours.
    /// </summary>
    Task<DateTime?> NewestProvisionedHourEnd(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default);
}

public readonly record struct HourDrop(int RowsDeleted, bool Completed);
