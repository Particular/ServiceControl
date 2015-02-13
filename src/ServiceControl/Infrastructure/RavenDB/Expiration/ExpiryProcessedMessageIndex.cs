namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;
    using ServiceControl.SagaAudit;

    public class ExpiryProcessedMessageIndex : AbstractMultiMapIndexCreationTask
    {
        public ExpiryProcessedMessageIndex()
        {
            AddMap<ProcessedMessage>(messages => from message in messages
                               where !(bool)message.MessageMetadata["IsRetried"]
                               select new
                               {
                                   ProcessedAt = message.ProcessedAt,
                               });
            AddMap<SagaSnapshot>(messages => from message in messages
                               select new
                               {
                                   ProcessedAt = message.ProcessedAt,
                               });
            AddMap<SagaHistory>(messages => from message in messages
                               select new
                               {
                                   ProcessedAt = MetadataFor(message).Value<DateTime>("Last-Modified"),
                               });

            DisableInMemoryIndexing = true;
        }
    }
}