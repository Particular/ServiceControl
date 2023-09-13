namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using RavenDb5;

    class EventLogDataStore : IEventLogDataStore
    {
        readonly DocumentStoreProvider storeProvider;

        public EventLogDataStore(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
        }

        public async Task Add(EventLogItem logItem)
        {
            using (var session = storeProvider.Store.OpenAsyncSession())
            {
                await session.StoreAsync(logItem);
                await session.SaveChangesAsync();
            }
        }

        public async Task<(IList<EventLogItem>, int, string)> GetEventLogItems(PagingInfo pagingInfo)
        {
            using (var session = storeProvider.Store.OpenAsyncSession())
            {
                var results = await session
                    .Query<EventLogItem>()
                    .Statistics(out var stats)
                    .OrderByDescending(p => p.RaisedAt, OrderingType.Double)
                    .Paging(pagingInfo)
                    .ToListAsync();

                return (results, stats.TotalResults, stats.ResultEtag.ToString());
            }
        }
    }
}
