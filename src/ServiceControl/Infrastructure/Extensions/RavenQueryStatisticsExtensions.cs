namespace ServiceControl.Infrastructure.Extensions
{
    using CompositeViews.Messages;
    using Raven.Client;

    internal static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this RavenQueryStatistics stats)
        {
            return new QueryStatsInfo(stats.IndexEtag, stats.TotalResults);
        }
    }
}