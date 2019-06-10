namespace ServiceControl.Audit.Auditing
{
    using NServiceBus;
    using System.Collections.Generic;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IMessageSession messageSession, IDictionary<string, object> metadata)
        {
            Headers = headers;
            MessageSession = messageSession;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }
        public IMessageSession MessageSession { get; }
        public IDictionary<string, object> Metadata { get; }
    }
}