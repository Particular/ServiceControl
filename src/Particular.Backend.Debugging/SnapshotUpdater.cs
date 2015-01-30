namespace Particular.Backend.Debugging
{
    using System.Collections.Generic;
    using Particular.Backend.Debugging.Enrichers;
    using ServiceControl.Shell.Api.Ingestion;

    class SnapshotUpdater
    {
        readonly IEnumerable<IEnrichAuditMessageSnapshots> enrichers;

        public SnapshotUpdater(IEnumerable<IEnrichAuditMessageSnapshots> enrichers)
        {
            this.enrichers = enrichers;
        }

        public void Update(AuditMessageSnapshot snapshot, HeaderCollection newHeaders)
        {
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(newHeaders, snapshot.MessageMetadata);
            }
            foreach (var newHeader in newHeaders)
            {
                snapshot.Headers[newHeader.Key] = newHeader.Value;
            }
        }
    }
}