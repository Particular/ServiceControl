namespace Particular.Backend.Debugging.Enrichers
{
    using System;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;

    public class MessageTypeEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, MessageSnapshot snapshot)
        {
            snapshot.MessageType = message.MessageType.Name;
            snapshot.IsSystemMessage = message.MessageType.IsSystem;
            string messageIntentText;
            if (message.Headers.TryGet("NServiceBus.MessageIntent", out messageIntentText))
            {
                snapshot.MessageIntent = (MessageIntentEnum) Enum.Parse(typeof(MessageIntentEnum), messageIntentText);
            }
        }
    }
}