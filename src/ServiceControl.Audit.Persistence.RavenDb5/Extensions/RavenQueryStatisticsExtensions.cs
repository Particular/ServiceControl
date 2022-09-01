namespace ServiceControl.Audit.Persistence.RavenDb.Extensions
{
    using Auditing.MessagesView;
    using Raven.Client.Documents.Session;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats)
        {
            return new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults);
        }
    }
}