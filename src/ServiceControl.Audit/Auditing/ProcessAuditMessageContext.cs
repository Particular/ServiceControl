namespace ServiceControl.Audit.Auditing
{
    using NServiceBus;
    using NServiceBus.Transport;

    class ProcessAuditMessageContext
    {
        public ProcessAuditMessageContext(MessageContext message, IMessageSession messageSession)
        {
            Message = message;
            MessageSession = messageSession;
        }

        public MessageContext Message { get; }

        public IMessageSession MessageSession { get; }
    }
}