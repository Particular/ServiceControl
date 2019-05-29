namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using Auditing;
    using Raven.Client.Indexes;

    class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProcessedMessage>
    {
        public ExpiryProcessedMessageIndex()
        {
            Map = messages => from message in messages
                select new
                {
                    ProcessedAt = message.ProcessedAt.Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}