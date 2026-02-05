namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

/// <summary>
/// Shared state for coordinating progressive ingestion throttling during retention cleanup.
/// Writers are reduced one at a time as cleanup continues.
/// </summary>
public class IngestionThrottleState(TimeProvider timeProvider)
{
    /// <summary>
    /// When cleanup started, or null if cleanup is not active.
    /// </summary>
    public DateTime? CleanupStartedAt { get; private set; }

    /// <summary>
    /// Whether cleanup is currently active.
    /// </summary>
    public bool IsCleanupActive => CleanupStartedAt.HasValue;

    /// <summary>
    /// Signals that cleanup has started.
    /// </summary>
    public void BeginCleanup()
    {
        CleanupStartedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Signals that cleanup has completed.
    /// </summary>
    public void EndCleanup()
    {
        CleanupStartedAt = null;
    }

    /// <summary>
    /// Calculates the current writer limit based on cleanup duration.
    /// Returns maxWriters if cleanup is not active.
    /// </summary>
    public int GetActiveWriterLimit(int maxWriters, TimeSpan throttleInterval, int minWriters)
    {
        if (!CleanupStartedAt.HasValue)
        {
            return maxWriters;
        }

        var elapsed = timeProvider.GetUtcNow().UtcDateTime - CleanupStartedAt.Value;
        var intervalsElapsed = (int)(elapsed / throttleInterval);
        var reduction = intervalsElapsed; // Reduce by 1 writer per interval

        var limit = Math.Max(minWriters, maxWriters - reduction);
        return limit;
    }

    /// <summary>
    /// Gets the current ingestion capacity as a percentage (0-100).
    /// </summary>
    public int GetCapacityPercent(int maxWriters, TimeSpan throttleInterval, int minWriters)
    {
        var activeLimit = GetActiveWriterLimit(maxWriters, throttleInterval, minWriters);
        return (int)Math.Round(100.0 * activeLimit / maxWriters);
    }
}
