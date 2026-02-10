namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using DbContexts;

public interface IPartitionManager
{
    /// <summary>
    /// Creates partitions for <paramref name="currentDate"/> through <paramref name="currentDate"/> + <paramref name="daysAhead"/>.
    /// Also fills any gaps between the last existing partition and <paramref name="currentDate"/>.
    /// </summary>
    Task EnsurePartitionsExist(AuditDbContextBase dbContext, DateTime currentDate, int daysAhead, CancellationToken ct);

    /// <summary>
    /// Drops the partition for the specified date for both ProcessedMessages and SagaSnapshots.
    /// </summary>
    Task DropPartition(AuditDbContextBase dbContext, DateTime partitionDate, CancellationToken ct);

    /// <summary>
    /// Returns dates of all partitions older than <paramref name="cutoff"/>.
    /// </summary>
    Task<List<DateTime>> GetExpiredPartitionDates(AuditDbContextBase dbContext, DateTime cutoff, CancellationToken ct);
}
