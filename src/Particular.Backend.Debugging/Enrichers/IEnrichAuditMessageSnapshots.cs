namespace Particular.Backend.Debugging.Enrichers
{
    using Particular.Operations.Ingestion.Api;

    public interface IEnrichAuditMessageSnapshots
    {
        void Enrich(IngestedMessage message, AuditMessageSnapshot snapshot);
    }
}