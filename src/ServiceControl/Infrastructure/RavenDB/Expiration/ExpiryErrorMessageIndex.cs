namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Indexes;

    public class ExpiryErrorMessageIndex : AbstractIndexCreationTask<FailedMessage>
    {
        public ExpiryErrorMessageIndex()
        {
            Map = messages => from message in messages
                where message.Status != FailedMessageStatus.Unresolved
                select new
                {
                    message.Status,
                    LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}