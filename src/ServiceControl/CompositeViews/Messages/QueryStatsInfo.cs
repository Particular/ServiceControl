namespace ServiceControl.CompositeViews.Messages
{
    using System;

    public struct QueryStatsInfo
    {
        public readonly string ETag;
        public readonly DateTime LastModified;
        public readonly int TotalCount;
        public readonly int HighestTotalCountOfAllTheInstances;

        public QueryStatsInfo(string eTag, DateTime lastModified, int totalCount)
        {
            ETag = eTag;
            LastModified = lastModified;
            TotalCount = totalCount;
            HighestTotalCountOfAllTheInstances = totalCount;
        }

        public QueryStatsInfo(string eTag, DateTime lastModified, int totalCount, int highestTotalCountOfAllTheInstances)
        {
            ETag = eTag;
            LastModified = lastModified;
            TotalCount = totalCount;
            HighestTotalCountOfAllTheInstances = highestTotalCountOfAllTheInstances;
        }

        public static readonly QueryStatsInfo Zero = new QueryStatsInfo(string.Empty, DateTime.MinValue, 0);
    }
}