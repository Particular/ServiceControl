namespace ServiceControl.Persistence.RavenDB
{
    using System.Linq;
    using EventLog;
    using Raven.Client.Documents.Indexes;

    // The event log query sorts by RaisedAt. Without this index, RavenDB serves it with an Auto/EventLogItems/ByRaisedAt
    // index, which has no per-index search engine setting.
    class EventLogItems_ByRaisedAt : AbstractIndexCreationTask<EventLogItem>
    {
        public EventLogItems_ByRaisedAt()
        {
            Map = docs =>

                from doc in docs
                select new
                {
                    doc.RaisedAt
                };
        }
    }
}
