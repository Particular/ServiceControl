namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;

    class EventLogDataStore(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager) : IEventLogDataStore
    {
        public async Task Add(EventLogItem logItem)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(logItem);

            // Retention on RavenDB is per-document expiry metadata stamped at write time, not a
            // sweep. It has to be set here, on the only write path, or items never expire.
            expirationManager.EnableExpiration(session, logItem);

            await session.SaveChangesAsync();
        }

        public async Task<(IList<EventLogItem>, long, string)> GetEventLogItems(
            PagingInfo pagingInfo, string knownVersion = null)
        {
            using var session = await sessionProvider.OpenSession();
            var results = await session
                .Query<EventLogItem>()
                .Statistics(out var stats)
                .OrderByDescending(p => p.RaisedAt)
                .Paging(pagingInfo)
                .ToListAsync();

            var version = stats.ResultEtag.ToString();

            // For robustness and consistency. Decide 304s at the controller level. 
            var unchanged = knownVersion is not null && knownVersion == version;

            return (unchanged ? null : results, stats.TotalResults, version);
        }
    }
}
