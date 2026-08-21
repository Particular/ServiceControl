namespace ServiceControl.Audit.Auditing
{
    /// <summary>
    /// Enriches an audit message as it is ingested. Implementations must be thread safe: ingestion
    /// runs several batches at once on storage that allows it, so one instance is called
    /// concurrently for messages in different batches.
    /// </summary>
    public interface IEnrichImportedAuditMessages
    {
        void Enrich(AuditEnricherContext context);
    }
}