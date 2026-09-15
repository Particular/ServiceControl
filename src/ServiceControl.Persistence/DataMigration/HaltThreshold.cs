namespace ServiceControl.Persistence.DataMigration;

public static class HaltThreshold
{
    // Both must be exceeded: the floor protects a tiny category from one bad row, and the
    // proportion protects a huge one from grinding through thousands of failures as "only a fraction".
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
