namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using Auditing;
    using Raven.Client.Documents.Indexes;

    public class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProcessedMessage>
    {
        public ExpiryProcessedMessageIndex()
        {
            Map = messages => from message in messages
                select new
                {
                    ProcessedAt = message.ProcessedAt.Ticks
                };

            // TODO: RAVEN5 - This API is missing
            //DisableInMemoryIndexing = true;
        }
    }
}