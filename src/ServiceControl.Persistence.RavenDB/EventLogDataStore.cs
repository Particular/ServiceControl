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
        public async Task Add(EventLogItem logItem, Guid eventId)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(
                logItem,
                EventLogItemIdGenerator.MakeDocumentId(logItem.Category, logItem.EventType, eventId));

            // Retention on RavenDB is per-document expiry metadata stamped at write time, not a
            // sweep. It has to be set here, on the only write path, or items never expire.
            expirationManager.EnableExpiration(session, logItem);

            await session.SaveChangesAsync();
        }

        public async Task<(IList<EventLogItemView>, long, string)> GetEventLogItems(
            PagingInfo pagingInfo, string knownVersion = null)
        {
            using var session = await sessionProvider.OpenSession();
            var documents = await session
                .Query<EventLogItem>()
                .Statistics(out var stats)
                .OrderByDescending(p => p.RaisedAt)
                .Paging(pagingInfo)
                .ToListAsync();

            var version = stats.ResultEtag.ToString();

            // For robustness and consistency. Decide 304s at the controller level.
            var unchanged = knownVersion is not null && knownVersion == version;

            if (unchanged)
            {
                return (null, stats.TotalResults, version);
            }

            // The id lives in document metadata rather than on the document, so it has to be read
            // from the session while it is still open.
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

            return (items, stats.TotalResults, version);
        }
    }
}
