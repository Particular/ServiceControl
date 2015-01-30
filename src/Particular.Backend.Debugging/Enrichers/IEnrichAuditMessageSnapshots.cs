namespace Particular.Backend.Debugging.Enrichers
{
    using ServiceControl.Shell.Api.Ingestion;

    public interface IEnrichAuditMessageSnapshots
    {
        void Enrich(HeaderCollection headers, SnapshotMetadata metadata);
    }
}