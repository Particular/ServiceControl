namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public abstract class ImportEnricher : IEnrichImportedMessages
    {
        public abstract Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);

        public virtual bool EnrichErrors => true;
        public virtual bool EnrichAudits => true;
    }
}