namespace ServiceControl.Persistence.DataMigration;

public static class HaltThreshold
{
    // Both must be exceeded: the floor ignores a few bad rows in a small category, the percentage a small share of a large one.
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
