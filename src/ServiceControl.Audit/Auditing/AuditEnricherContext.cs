namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Transport;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<ICommand> outgoingCommands, IList<TransportOperation> outgoingSends, IDictionary<string, object> metadata)
        {
            Headers = headers;
            this.outgoingCommands = outgoingCommands;
            this.outgoingSends = outgoingSends;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, object> Metadata { get; }

        public void AddForSend(ICommand command)
        {
            outgoingCommands.Add(command);
        }

        public void AddForSend(TransportOperation transportOperation)
        {
            outgoingSends.Add(transportOperation);
        }

        readonly IList<ICommand> outgoingCommands;
        readonly IList<TransportOperation> outgoingSends;
    }
}