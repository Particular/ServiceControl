namespace ServiceControl.CompositeViews.Messages
{
    public struct QueryStatsInfo
    {
        public readonly string ETag;
        public readonly int TotalCount;
        public readonly int HighestTotalCountOfAllTheInstances;

        public QueryStatsInfo(string eTag, int totalCount, int? highestTotalCountOfAllTheInstances = null)
        {
            ETag = eTag;
            TotalCount = totalCount;
            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new QueryStatsInfo(string.Empty, 0);
    }
}