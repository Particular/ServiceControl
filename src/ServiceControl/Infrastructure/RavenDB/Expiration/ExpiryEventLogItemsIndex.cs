namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.EventLog;

    public class ExpiryEventLogItemsIndex : AbstractIndexCreationTask<EventLogItem>
    {
        public ExpiryEventLogItemsIndex()
        {
            Map = messages => from message in messages
                select new
                {
                    LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}