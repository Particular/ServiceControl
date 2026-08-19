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
        /// </summary>
        public static QueryStatsInfo ToPagedQueryStatsInfo<TRow>(this QueryStatistics stats, IEnumerable<TRow> page, Func<TRow, string> id) =>
            new(stats.ResultEtag is { } resultEtag
                    ? DataVersion.OverRows([("index", resultEtag), ("total", stats.TotalResults)], page, row => [id(row)])
                    : DataVersion.None,
                stats.TotalResults,
                stats.IsStale);

        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats) =>
            new(stats.ResultEtag is { } resultEtag ? DataVersion.FromToken(resultEtag) : DataVersion.None,
                stats.TotalResults,
                stats.IsStale);

        public static QueryStatsInfo ToQueryStatsInfo(this Raven.Client.Documents.Queries.QueryResult queryResult) =>
            new(DataVersion.FromToken(queryResult.ResultEtag), queryResult.TotalResults, queryResult.IsStale);
    }
}
