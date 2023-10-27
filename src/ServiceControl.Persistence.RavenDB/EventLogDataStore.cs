﻿namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Persistence.Infrastructure;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;

    class EventLogDataStore : IEventLogDataStore
    {
        readonly IDocumentStore documentStore;

        public EventLogDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task Add(EventLogItem logItem)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(logItem);
                await session.SaveChangesAsync();
            }
        }

        public async Task<(IList<EventLogItem>, int, string)> GetEventLogItems(PagingInfo pagingInfo)
        {
            using (var session = documentStore.OpenAsyncSession())
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
