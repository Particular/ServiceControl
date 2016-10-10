namespace ServiceControl.Operations
{
    using System.Collections.Generic;

    public abstract class ImportEnricher : IEnrichImportedMessages
    {
        public abstract void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);

        public virtual bool EnrichErrors => true;
        public virtual bool EnrichAudits => true;
    }
}