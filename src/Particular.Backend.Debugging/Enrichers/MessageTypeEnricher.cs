namespace Particular.Backend.Debugging.Enrichers
{
    using Particular.Operations.Ingestion.Api;

    public class MessageTypeEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, AuditMessageSnapshot snapshot)
        {
            snapshot.MessageType = message.MessageType.Name;
            snapshot.IsSystemMessage = message.MessageType.IsSystem;
        }
    }
}