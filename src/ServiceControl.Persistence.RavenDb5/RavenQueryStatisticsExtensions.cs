namespace ServiceControl.Persistence
{
    using Raven.Client;
    using ServiceControl.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this RavenQueryStatistics stats)
        {
            return new QueryStatsInfo(stats.IndexEtag, stats.TotalResults, stats.IsStale);
        }
        public static QueryStatsInfo ToQueryStatsInfo(this Raven.Abstractions.Data.QueryResult queryResult)
        {
            return new QueryStatsInfo(queryResult.IndexEtag, queryResult.TotalResults, queryResult.IsStale);
        }
    }
}