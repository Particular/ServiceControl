namespace Particular.Backend.Debugging
{
    using System;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class SuccessfulMessageImporter : IProcessSuccessfulMessages
    {
        readonly IStoreMessageSnapshots snapshotStore;
        readonly SnapshotUpdater snapshotUpdater;

        public SuccessfulMessageImporter(IStoreMessageSnapshots snapshotStore, SnapshotUpdater snapshotUpdater)
        {
            this.snapshotStore = snapshotStore;
            this.snapshotUpdater = snapshotUpdater;
        }

        public void ProcessSuccessful(IngestedMessage message)
        {
            snapshotStore.StoreOrUpdate(message.UniqueId,
                @new =>
                {
                    @new.Initialize(message.Id, message.UniqueId, MessageStatus.Successful);
                    UpdateSnapshot(message, @new);
                },
                existing => UpdateSnapshot(message, existing));
        }

        void UpdateSnapshot(IngestedMessage message, MessageSnapshot snapshot)
        {
            snapshot.AttemptedAt = GuessProcessingAttemptTime(message);
            snapshotUpdater.Update(snapshot, message);
        }

        static DateTime GuessProcessingAttemptTime(IngestedMessage message)
        {
            DateTime attemptedAt;
            string processedAt;
            if (message.Headers.TryGet("NServiceBus.ProcessingEnded", out processedAt))
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