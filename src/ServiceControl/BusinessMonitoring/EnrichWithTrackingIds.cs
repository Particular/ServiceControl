namespace ServiceControl.BusinessMonitoring
{
    using Contracts.Operations;
    using Operations;

    public class EnrichWithTrackingIds : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            string conversationId;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.ConversationId, out conversationId))
            {
                message.Metadata.Add("ConversationId", conversationId);
            }

            string relatedToId;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.RelatedTo, out relatedToId))
            {
                message.Metadata.Add("RelatedToId", relatedToId);
            }
        }
    }
}