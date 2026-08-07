namespace ServiceControl.Persistence.Infrastructure
{
    public readonly struct QueryStatsInfo
    {
        public readonly DataVersion Version;
        public readonly long TotalCount;
        public readonly long HighestTotalCountOfAllTheInstances;
        public readonly bool IsStale;

        public QueryStatsInfo(DataVersion version, long totalCount, bool isStale, long? highestTotalCountOfAllTheInstances = null)
        {
            Version = version;
            TotalCount = totalCount;
            IsStale = isStale;

            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances ?? totalCount;
        }

        public static readonly QueryStatsInfo Zero = new(DataVersion.None, 0, false);
    }
}
