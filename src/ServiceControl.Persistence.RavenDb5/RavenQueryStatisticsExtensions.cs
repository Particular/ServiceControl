namespace ServiceControl.Persistence
{
    using Raven.Client.Documents.Session;
    using ServiceControl.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats)
        {
            return new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults, stats.IsStale);
        }

        public static QueryStatsInfo ToQueryStatsInfo(this Raven.Client.Documents.Queries.QueryResult queryResult)
        {
            return new QueryStatsInfo(queryResult.ResultEtag.ToString(), queryResult.TotalResults, queryResult.IsStale);
        }
    }
}