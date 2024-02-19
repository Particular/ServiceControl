namespace Throughput.Persistence;

public readonly struct QueryStatsInfo(string eTag, int totalCount, int? highestTotalCountOfAllTheInstances = null)
{
    public readonly string ETag = eTag;
    public readonly int TotalCount = totalCount;
    public readonly int HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;

    public static readonly QueryStatsInfo Zero = new(string.Empty, 0);
}