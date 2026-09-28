namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System.Globalization;
    using Auditing.MessagesView;
    using Raven.Client.Documents.Session;
    using ServiceControl.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats) =>
            new(stats.ResultEtag is { } resultEtag ? DataVersion.FromToken(resultEtag) : DataVersion.None,
                stats.TotalResults);
    }
}