namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;

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