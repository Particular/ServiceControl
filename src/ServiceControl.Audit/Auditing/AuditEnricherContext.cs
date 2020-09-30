namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using NServiceBus;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<ICommand> outgoingCommands, IDictionary<string, string> searchTerms, ProcessedMessage processedMessage)
        {
            Headers = headers;
            SearchTerms = searchTerms;
            ProcessedMessage = processedMessage;
            this.outgoingCommands = outgoingCommands;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, string> SearchTerms { get; }

        public ProcessedMessage ProcessedMessage { get; }

        public void AddForSend(ICommand command)
        {
            outgoingCommands.Add(command);
        }

        IList<ICommand> outgoingCommands;
    }
}