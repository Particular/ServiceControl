namespace ServiceControl.Persistence.Infrastructure
{
    public readonly struct QueryStatsInfo
    {
        public readonly DataVersion Version;
        public readonly long TotalCount;
        public readonly long HighestTotalCountOfAllTheInstances;

        public QueryStatsInfo(DataVersion version, long totalCount, long? highestTotalCountOfAllTheInstances = null)
        {
            Version = version;
            TotalCount = totalCount;

            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new(DataVersion.None, 0);
    }
}