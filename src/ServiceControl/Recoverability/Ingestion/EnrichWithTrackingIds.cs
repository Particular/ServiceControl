namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;
    using ServiceControl.Operations;

    class EnrichWithTrackingIds : IEnrichImportedErrorMessages
    {
        public void Enrich(ErrorEnricherContext context)
        {
            if (context.Headers.TryGetValue(Headers.ConversationId, out var conversationId))
            {
                context.Metadata.Add("ConversationId", conversationId);
            }

            if (context.Headers.TryGetValue(Headers.RelatedTo, out var relatedToId))
            {
                context.Metadata.Add("RelatedToId", relatedToId);
            }
        }
    }
}