﻿namespace ServiceControl.Audit.Infrastructure.Extensions
{
    using Auditing.MessagesView;
    using Raven.Client;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this RavenQueryStatistics stats)
        {
            return new QueryStatsInfo(stats.IndexEtag, stats.TotalResults);
        }
    }
}