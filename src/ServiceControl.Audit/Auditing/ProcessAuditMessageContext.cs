namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;
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

        public TaskCompletionSource<bool> Completed { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}