namespace ServiceControl.Persistence
{
    using Raven.Client;
    using Raven.Client.Documents.Session;
    using ServiceControl.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats)
        {
            return new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults, stats.IsStale);
        }

        //TODO: This method can likely be removed
        //public static QueryStatsInfo ToQueryStatsInfo(this Raven.Abstractions.Data.QueryResult queryResult)
        //{
        //    return new QueryStatsInfo(queryResult.IndexEtag, queryResult.TotalResults, queryResult.IsStale);
        //}
    }
}