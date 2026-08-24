namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System.Globalization;
    using Auditing.MessagesView;
    using Raven.Client.Documents.Session;

    static class RavenQueryStatisticsExtensions
    {
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats) =>
            new(stats.ResultEtag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, stats.TotalResults);
    }
}
