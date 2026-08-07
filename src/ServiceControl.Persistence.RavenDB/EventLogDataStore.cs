namespace ServiceControl.Persistence.RavenDB
{
    using System;
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

            // Version 7 rather than a random GUID so the final segment is time-ordered, which keeps
            // documents written together adjacent in the id index.
            await session.StoreAsync(
                logItem,
                EventLogItemIdGenerator.MakeDocumentId(logItem.Category, logItem.EventType, Guid.CreateVersion7()));

            // Retention on RavenDB is per-document expiry metadata stamped at write time, not a
            // sweep. It has to be set here, on the only write path, or items never expire.
            expirationManager.EnableExpiration(session, logItem);

            await session.SaveChangesAsync();
        }

        public async Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
            PagingInfo pagingInfo, DataVersion knownVersion = default)
        {
            using var session = await sessionProvider.OpenSession();
            var documents = await session
                .Query<EventLogItem>()
                .Statistics(out var stats)
                .OrderByDescending(p => p.RaisedAt)
                .Paging(pagingInfo)
                .ToListAsync();

            var queryStats = stats.ToQueryStatsInfo();

            // The validator comes off the query statistics, so the page cannot be
            // skipped. Only the projection below is saved.
            if (knownVersion.Matches(queryStats.Version))
            {
                return QueryResult<IList<EventLogItemView>>.Unchanged(queryStats);
            }

            // The id lives in document metadata rather than on the document
            var items = documents.ConvertAll(document => new EventLogItemView
            {
                Id = session.Advanced.GetDocumentId(document),
                Description = document.Description,
                Severity = document.Severity,
                RaisedAt = document.RaisedAt,
                RelatedTo = document.RelatedTo,
                Category = document.Category,
                EventType = document.EventType
            });

            return new QueryResult<IList<EventLogItemView>>(items, queryStats);
        }
    }
}
