namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Operations;

    abstract class AuditImportEnricher : IEnrichImportedAuditMessages
    {
        public abstract Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata);
    }
}