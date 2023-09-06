namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using EventLog;
    using Raven.Client.Documents.Indexes;

    class ExpiryEventLogItemsIndex : AbstractIndexCreationTask<EventLogItem>
    {
        public ExpiryEventLogItemsIndex()
        {
            Map = messages => from message in messages
                              select new
                              {
                                  LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                              };
        }
    }
}