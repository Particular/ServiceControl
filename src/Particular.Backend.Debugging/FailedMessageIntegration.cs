namespace Particular.Backend.Debugging
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Shell.Api.Ingestion;

    class FailedMessageIntegration :
        IHandleMessages<ImportFailedMessage>,
        IHandleMessages<MessageFailureResolved>,
        IHandleMessages<FailedMessageArchived>
    {
        readonly IStoreMessageSnapshots store;
        readonly SnapshotUpdater snapshotUpdater;

        public FailedMessageIntegration(IStoreMessageSnapshots store, SnapshotUpdater snapshotUpdater)
        {
            this.store = store;
            this.snapshotUpdater = snapshotUpdater;
        }

        public void Handle(ImportFailedMessage failedMessage)
        {
            store.StoreOrUpdate(failedMessage.UniqueMessageId,
                @new =>
                {
                    @new.Initialize(failedMessage.UniqueMessageId, MessageStatus.Failed);
                    snapshotUpdater.Update(@new, new HeaderCollection(failedMessage.PhysicalMessage.Headers));
                },
                existing =>
                {
                    if (existing.Status == MessageStatus.Failed)
                    {
                        existing.Status = MessageStatus.RepeatedFailure;
                    }
                    snapshotUpdater.Update(existing, new HeaderCollection(failedMessage.PhysicalMessage.Headers));
                });
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
