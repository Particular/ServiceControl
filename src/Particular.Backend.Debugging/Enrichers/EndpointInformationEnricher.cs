namespace Particular.Backend.Debugging.Enrichers
{
    using Particular.Operations.Ingestion.Api;

    public class EndpointInformationEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, AuditMessageSnapshot snapshot)
        {
            snapshot.ReceivingEndpointName = message.ProcessedAt.EndpointName;
        }
    }
}