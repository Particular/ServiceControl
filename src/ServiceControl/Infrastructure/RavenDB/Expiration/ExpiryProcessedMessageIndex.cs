namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using Contracts.Operations;
    using MessageAuditing;
    using Raven.Client.Indexes;

    public class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProcessedMessage>
    {
        public ExpiryProcessedMessageIndex()
        {
            Map = messages => from message in messages
                select new
                {
                    Status = (bool)message.MessageMetadata["IsRetried"] ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                    ProcessedAt = message.ProcessedAt.Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}