namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;

    public class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProcessedMessage>
    {  
        public ExpiryProcessedMessageIndex()
        {
            Map = (messages => from message in messages
                select new 
                {
                    MessageId = (string) message.MessageMetadata["MessageId"],
                    ProcessedAt = message.ProcessedAt,
                });

            DisableInMemoryIndexing = true;
        }
    }
}