namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The two rules that decide a category has lost too much to keep copying.
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

    /// <summary>
    /// Whether more than half the rows were skipped, which catches a category too small ever to reach
    /// <see cref="Exceeded" />'s floor. Ask it only at the end of a run, over that run or over every run
    /// together. Part way through, the same ratio is no more than a bad first batch.
    /// </summary>
    /// <param name="skippedCount">Rows skipped as faults.</param>
    /// <param name="totalCount">Rows dealt with over the same stretch as <paramref name="skippedCount" />.</param>
    /// <returns>True when the skipped rows are more than half, which means the category stops.</returns>
    public static bool MostOfItWasLost(long skippedCount, long totalCount) =>
        totalCount > 0 && skippedCount * 2 > totalCount;
}
