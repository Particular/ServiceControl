namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;
    using ServiceControl.Operations;

    public class EnrichWithTrackingIds : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            string conversationId;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.ConversationId, out conversationId))
            {
                message.Metadata.Add("ConversationId", conversationId);
            }

            string relatedToId;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.RelatedTo, out relatedToId))
            {
                message.Metadata.Add("RelatedToId", relatedToId);
            }
        }
    }
}