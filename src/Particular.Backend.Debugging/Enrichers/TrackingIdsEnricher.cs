namespace Particular.Backend.Debugging.Enrichers
{
    using ServiceControl.Shell.Api.Ingestion;

    public class TrackingIdsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(HeaderCollection headers, SnapshotMetadata metadata)
        {
            string conversationId;

            if (headers.TryGet(NServiceBus.Headers.ConversationId, out conversationId))
            {
                metadata.Set("ConversationId", conversationId);
            }

            string relatedToId;

            if (headers.TryGet(NServiceBus.Headers.RelatedTo, out relatedToId))
            {
                metadata.Set("RelatedToId", relatedToId);
            }
        }
    }
}