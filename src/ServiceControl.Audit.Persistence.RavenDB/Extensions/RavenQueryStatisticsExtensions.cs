namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System.Globalization;
    using Auditing.MessagesView;
    using Raven.Client.Documents.Session;

    static class RavenQueryStatisticsExtensions
    {
        /// <summary>
        /// RavenDB's result etag hashes the state of the index behind the query: every collection's last
        /// document and tombstone etag, how far the index has processed, and its definition. It therefore
        /// moves on any write the query could see, which is all a validator has to do, because a client only
        /// ever sends one back on a request for the same URL.
        /// </summary>
        public static QueryStatsInfo ToQueryStatsInfo(this QueryStatistics stats) =>
            new(stats.ResultEtag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, stats.TotalResults);
    }
}
