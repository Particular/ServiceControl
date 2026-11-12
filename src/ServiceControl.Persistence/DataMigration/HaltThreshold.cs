namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The rule that stops a category early because it has lost too much to keep copying.
/// </summary>
public static class HaltThreshold
{
    /// <summary>
    /// Whether the skipped rows are enough to stop the category. Both limits have to be passed, so the floor
    /// ignores a few bad rows in a small category and the percentage ignores a small share of a large one.
    /// </summary>
    /// <param name="skippedCount">Rows skipped as faults. A skip the product would have dropped anyway does not belong here.</param>
    /// <param name="totalCount">Rows dealt with over the same stretch as <paramref name="skippedCount" />, copied, skipped and already present alike.</param>
    /// <param name="percentThreshold">The share of skipped rows, as a percentage, that has to be passed.</param>
    /// <param name="minimumFloor">The number of skipped rows that has to be passed before the percentage counts at all.</param>
    /// <returns>True when both limits are passed, which means the category stops.</returns>
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
