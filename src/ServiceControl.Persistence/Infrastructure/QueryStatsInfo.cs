namespace ServiceControl.Persistence.Infrastructure
{
    public struct QueryStatsInfo
    {
        public readonly string ETag;
        public readonly long TotalCount;
        public readonly long HighestTotalCountOfAllTheInstances;
        public readonly bool IsStale;

        public QueryStatsInfo(string eTag, long totalCount, bool isStale, long? highestTotalCountOfAllTheInstances = null)
        {
            ETag = eTag;
            TotalCount = totalCount;
            IsStale = isStale;

            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new QueryStatsInfo(string.Empty, 0, false);
    }
}