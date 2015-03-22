namespace Particular.Backend.Debugging
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;

    class FailedMessageIntegration :
        IHandleMessages<MessageFailureResolved>,
        IHandleMessages<FailedMessageArchived>
    {
        readonly IStoreMessageSnapshots store;

        public FailedMessageIntegration(IStoreMessageSnapshots store)
        {
            this.store = store;
        }

        public void Handle(FailedMessageArchived message)
        {
            store.UpdateIfExists(message.FailedMessageId, existing => existing.Status = MessageStatus.ArchivedFailure);
        }

        public void Handle(MessageFailureResolved message)
        {
            store.UpdateIfExists(message.FailedMessageId, existing => existing.Status = MessageStatus.ResolvedSuccessfully);
        }
    }
}
