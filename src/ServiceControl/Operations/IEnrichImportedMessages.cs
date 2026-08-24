namespace ServiceControl.Operations
{
    /// <summary>
    /// Enriches an error message as it is ingested. Implementations must be thread safe: ingestion
    /// runs several batches at once on storage that allows it, so one instance is called
    /// concurrently for messages in different batches.
    /// </summary>
    public interface IEnrichImportedErrorMessages
    {
        void Enrich(ErrorEnricherContext context);
    }
}