namespace Particular.Backend.Debugging.Enrichers
{
    using Particular.Operations.Ingestion.Api;

    public class TrackingIdsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, MessageSnapshot snapshot)
        {
            var headers = message.Headers;
            string conversationId;

            if (headers.TryGet(NServiceBus.Headers.ConversationId, out conversationId))
            {
                snapshot.ConversationId = conversationId;
            }

            string relatedToId;

            if (headers.TryGet(NServiceBus.Headers.RelatedTo, out relatedToId))
            {
                snapshot.RelatedToId = relatedToId;
            }
        }
    }
}