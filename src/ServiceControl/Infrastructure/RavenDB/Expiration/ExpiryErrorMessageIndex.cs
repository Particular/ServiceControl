namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class ExpiryErrorMessageIndex : AbstractIndexCreationTask<FailedMessage>
    {
        public ExpiryErrorMessageIndex()
        {
            Map = messages => from message in messages
                where message.Status != FailedMessageStatus.Unresolved
                select new
                {
                    Status = message.Status,
                    LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}