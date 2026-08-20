namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using Raven.Client.Documents.Session;
    using ServiceControl.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        /// <summary>
        /// For a paged query. The index etag covers whether the data moved, and the row ids cover which
        /// rows this page renders. The etag alone cannot: it is a function of index and collection state,
        /// so every filter, page and sort over one index shares it.
        /// <para>
        /// <paramref name="query"/> A page with no rows contributes no row terms at all, so without it a 
        /// page past the end and any other empty view of the same index share a version.
        /// </para>
        /// </summary>
        public static QueryStatsInfo ToPagedQueryStatsInfo<TRow>(this QueryStatistics stats, IEnumerable<TRow> page, Func<TRow, string> id, params (string Name, object Value)[] query) =>
            new(stats.ResultEtag is { } resultEtag
                    ? DataVersion.OverRows([("index", resultEtag), ("total", stats.TotalResults), .. query], page, row => [id(row)])
                    : DataVersion.None,
                stats.TotalResults,
                stats.IsStale);

        /// <summary>
        /// For a response whose whole content is its count, which has no rows to be named by.
        /// <paramref name="query"/> is the only thing separating one filter from another here: leave it out
        /// and a caller holding the count for one filter is told another filter's count is still current.
        /// </summary>
        public static QueryStatsInfo ToCountQueryStatsInfo(this Raven.Client.Documents.Queries.QueryResult queryResult, params (string Name, object Value)[] query) =>
            new(DataVersion.Compose([("index", queryResult.ResultEtag), ("total", queryResult.TotalResults), .. query]),
                queryResult.TotalResults,
                queryResult.IsStale);
    }
}
