namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using EventLog;
    using Raven.Client.Indexes;

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