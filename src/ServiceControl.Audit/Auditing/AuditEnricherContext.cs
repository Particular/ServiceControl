namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using NServiceBus;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<ICommand> outgoingCommands, ProcessedMessageData data)
        {
            Headers = headers;
            this.outgoingCommands = outgoingCommands;
            Metadata = data;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public ProcessedMessageData Metadata { get; }

        public void AddForSend(ICommand command)
        {
            outgoingCommands.Add(command);
        }

        IList<ICommand> outgoingCommands;
    }
}