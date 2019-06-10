namespace ServiceControl.Audit.Auditing
{
    using NServiceBus;
    using System.Collections.Generic;

    class AuditEnricherContext
    {
        public IReadOnlyDictionary<string, string> Headers { get; set; }
        public IMessageSession MessageSession { get; set; }
        public IDictionary<string, object> Metadata { get; set; }
    }
}