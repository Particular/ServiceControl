namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The rule that stops a category early because it has lost too much to keep copying.
/// </summary>
public static class HaltThreshold
{
    /// <summary>
    /// Returns true, which stops the category, when skippedCount passes minimumFloor and its share of totalCount passes percentThreshold percent. 
    /// The floor ignores a few bad rows in a small category, and the percentage ignores a small share of a large one. 
    /// skippedCount holds fault skips only, never a harmless skip that <see cref="MigrationSkipReasonExtensions.IsBenign" /> accepts, and 
    /// totalCount holds every row dealt with over the same stretch, copied, skipped and already present alike.
    /// </summary>
    public static bool Exceeded(long skippedCount, long totalCount, int percentThreshold, int minimumFloor)
    {
        if (skippedCount <= minimumFloor || totalCount == 0)
        {
            return false;
        }

        var percent = skippedCount * 100m / totalCount;
        return percent > percentThreshold;
    }
}
