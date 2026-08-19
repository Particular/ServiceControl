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

        /// <summary>
        /// For a result that cannot be stale (when queries can
        /// run against an index that has not caught up).
        /// </summary>
        public static QueryStatsInfo Fresh(DataVersion version, long totalCount) =>
            new(version, totalCount, isStale: false);

        public static readonly QueryStatsInfo Zero = Fresh(DataVersion.None, 0);
    }
}
