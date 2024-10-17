namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;

    class EventLogDataStore(IRavenSessionProvider sessionProvider) : IEventLogDataStore
    {
        public async Task Add(EventLogItem logItem)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(logItem);
            await session.SaveChangesAsync();
        }

        public async Task<(IList<EventLogItem>, long, string)> GetEventLogItems(PagingInfo pagingInfo)
        {
            using var session = await sessionProvider.OpenSession();
            var results = await session
                .Query<EventLogItem>()
                .Statistics(out var stats)
                .OrderByDescending(p => p.RaisedAt)
                .Paging(pagingInfo)
                .ToListAsync();

            return (results, stats.TotalResults, stats.ResultEtag.ToString());
        }
    }
}
