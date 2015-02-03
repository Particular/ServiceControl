namespace Particular.Backend.Debugging
{
    using System.Collections.Generic;
    using Particular.Backend.Debugging.Enrichers;
    using Particular.Operations.Ingestion.Api;

    public class SnapshotUpdater
    {
        readonly IEnumerable<IEnrichAuditMessageSnapshots> enrichers;

        public SnapshotUpdater(IEnumerable<IEnrichAuditMessageSnapshots> enrichers)
        {
            this.enrichers = enrichers;
        }

        public void Update(AuditMessageSnapshot snapshot, IngestedMessage message)
        {
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(message, snapshot);
            }
            foreach (var newHeader in message.Headers)
            {
                snapshot.Headers[newHeader.Key] = newHeader.Value;
            }
        }

    }
}