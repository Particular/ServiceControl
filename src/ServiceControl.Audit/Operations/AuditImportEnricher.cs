namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    abstract class AuditImportEnricher : IEnrichImportedAuditMessages
    {
        public abstract Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);
    }
}