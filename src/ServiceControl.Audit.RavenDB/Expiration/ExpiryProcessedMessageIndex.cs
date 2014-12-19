namespace ServiceControl.ProductionDebugging.RavenDB.Expiration
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.ProductionDebugging.RavenDB.Data;

    public class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProdDebugMessage>
    {  
        public ExpiryProcessedMessageIndex()
        {
            Map = (messages => from message in messages
                select new 
                {
                    MessageId = (string) message.MessageMetadata["MessageId"],
                    Status = message.Status,
                    ProcessedAt = message.AttemptedAt,
                });

            DisableInMemoryIndexing = true;
        }
    }
}