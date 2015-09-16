namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;
    using ServiceControl.SagaAudit;

    public class ExpiryProcessedMessageIndex : AbstractMultiMapIndexCreationTask
    {  
        public ExpiryProcessedMessageIndex()
        {
            AddMap<ProcessedMessage>(messages => from message in messages
                                                 select new
                                                 {
                                                     ProcessedAt = message.ProcessedAt,
                                                 });
            AddMap<SagaHistory>(sagaHistories => from sagaHistory in sagaHistories
                                             select new
                                             {
                                                 ProcessedAt = MetadataFor(sagaHistory)["Last-Modified"],
                                             });

            DisableInMemoryIndexing = true;
        }
    }
}