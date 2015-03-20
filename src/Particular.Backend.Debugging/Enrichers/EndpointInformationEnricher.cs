namespace Particular.Backend.Debugging.Enrichers
{
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class EndpointInformationEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, MessageSnapshot snapshot)
        {
            snapshot.ReceivingEndpoint = new EndpointDetails()
            {
                HostId = message.ProcessedAt.HostId,
                Name = message.ProcessedAt.EndpointName
            };
            snapshot.SendingEndpoint = new EndpointDetails()
            {
                HostId = message.SentFrom.HostId,
                Name = message.SentFrom.EndpointName
            };
        }
    }
}