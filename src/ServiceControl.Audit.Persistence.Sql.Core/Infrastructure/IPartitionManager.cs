namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using DbContexts;

public interface IPartitionManager
{
    /// <summary>
    /// Creates hourly partitions from <paramref name="currentHour"/> through <paramref name="currentHour"/> + <paramref name="hoursAhead"/>.
    /// </summary>
    Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentHour, int hoursAhead, CancellationToken ct);

    /// <summary>
    /// Drops the partition for the specified hour for both ProcessedMessages and SagaSnapshots.
    /// </summary>
    Task DropPartition(AuditDbContextBase dbContext, DateTime partitionHour, CancellationToken ct);

    /// <summary>
    /// Returns hour-precision timestamps of all partitions older than <paramref name="cutoff"/>.
    /// </summary>
    Task<List<DateTime>> GetExpiredPartitions(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct);
}
