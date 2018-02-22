namespace ServiceControl.Infrastructure.Extensions
{
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;

    internal static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this RavenQueryStatistics stats)
        {
            return new QueryStatsInfo(stats.IndexEtag, stats.TotalResults);
        }
    }
}