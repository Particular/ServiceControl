namespace Particular.Backend.Debugging
{
    using System;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class FailedMessageImporter : IProcessFailedMessages
    {
        readonly IStoreMessageSnapshots snapshotStore;
        readonly SnapshotUpdater snapshotUpdater;

        public FailedMessageImporter(IStoreMessageSnapshots snapshotStore, SnapshotUpdater snapshotUpdater)
        {
            this.snapshotStore = snapshotStore;
            this.snapshotUpdater = snapshotUpdater;
        }

        public void ProcessFailed(IngestedMessage message)
        {
            snapshotStore.StoreOrUpdate(message.UniqueId,
                @new =>
                {
                    @new.Initialize(message.Id, message.UniqueId, MessageStatus.Failed);
                    UpdateSnapshot(message, @new);
                },
                existing =>
                {
                    if (existing.Status == MessageStatus.Failed)
                    {
                        existing.Status = MessageStatus.RepeatedFailure;
                    }
                    UpdateSnapshot(message, existing);
                });
        }

        void UpdateSnapshot(IngestedMessage message, AuditMessageSnapshot snapshot)
        {
            snapshot.AttemptedAt = GuessProcessingAttemptTime(message);
            snapshotUpdater.Update(snapshot, message);
        }

        static DateTime GuessProcessingAttemptTime(IngestedMessage message)
        {
            DateTime attemptedAt;
            string processedAt;
            if (message.Headers.TryGet("NServiceBus.TimeOfFailure", out processedAt))
            {
                attemptedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                attemptedAt = DateTime.UtcNow; //best guess    
            }
            return attemptedAt;
        }
    }
}