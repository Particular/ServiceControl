namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Linq;
    using MessageAuditing;
    using Raven.Client.Indexes;

    public class ExpiryProcessedMessageIndex : AbstractIndexCreationTask<ProcessedMessage>
    {
        public ExpiryProcessedMessageIndex()
        {
            Map = messages => from message in messages
                              let bodyStored = !(bool)message.MessageMetadata["BodyNotStored"]
                              let messageId = (string)message.MessageMetadata["MessageId"]
                              let bodyUrl = bodyStored ? $"messagebodies/{messageId}" : null
                              select new
                              {
                                  ProcessedAt = message.ProcessedAt.Ticks,
                                  _ = CreateField("BodyUrl", bodyUrl, true, false)
                              };

            DisableInMemoryIndexing = true;
        }
    }
}