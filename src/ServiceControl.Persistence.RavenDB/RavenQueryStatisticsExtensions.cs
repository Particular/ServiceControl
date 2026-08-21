namespace ServiceControl.Persistence
{
    using Raven.Client.Documents.Session;
    using ServiceControl.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats) =>
            new(stats.ResultEtag is { } resultEtag ? DataVersion.FromToken(resultEtag) : DataVersion.None,
                stats.TotalResults);

        public static QueryStatsInfo ToQueryStatsInfo(this Raven.Client.Documents.Queries.QueryResult queryResult) =>
            new(DataVersion.FromToken(queryResult.ResultEtag),
                queryResult.TotalResults);
    }
}
