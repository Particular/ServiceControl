namespace ServiceControl.Audit.Auditing.MessagesView
{
    public struct QueryStatsInfo
    {
        public readonly string ETag;
        public readonly long TotalCount;
        public readonly long HighestTotalCountOfAllTheInstances;

        public QueryStatsInfo(string eTag, long totalCount, long? highestTotalCountOfAllTheInstances = null)
        {
            ETag = eTag;
            TotalCount = totalCount;
            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new QueryStatsInfo(string.Empty, 0);
    }
}