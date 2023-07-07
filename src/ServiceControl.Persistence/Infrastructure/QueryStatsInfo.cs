namespace ServiceControl.Persistence.Infrastructure
{
    public struct QueryStatsInfo
    {
        public readonly string ETag;
        public readonly int TotalCount;
        public readonly int HighestTotalCountOfAllTheInstances;
        public readonly bool IsStale;

        public QueryStatsInfo(string eTag, int totalCount, bool isStale, int? highestTotalCountOfAllTheInstances = null)
        {
            ETag = eTag;
            TotalCount = totalCount;
            IsStale = isStale;

            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new QueryStatsInfo(string.Empty, 0, false);
    }
}