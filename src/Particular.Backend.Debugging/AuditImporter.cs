namespace Particular.Backend.Debugging
{
    using System;
    using NServiceBus;
    using Particular.Backend.AuditIngestion.Api;
    using ServiceControl.Contracts.Operations;

    class AuditImporter : IProcessAuditMessages
    {
        readonly IStoreMessageSnapshots snapshotStore;
        readonly SnapshotUpdater snapshotUpdater;

        public AuditImporter(IStoreMessageSnapshots snapshotStore, SnapshotUpdater snapshotUpdater)
        {
            this.snapshotStore = snapshotStore;
            this.snapshotUpdater = snapshotUpdater;
        }

        public void Process(IngestedAuditMessage message)
        {
            snapshotStore.StoreOrUpdate(message.UniqueId,
                @new =>
                {
                    @new.Initialize(message.UniqueId, MessageStatus.Successful);
                    UpdateSnapshot(message, @new);
                },
                existing => UpdateSnapshot(message, existing));
        }

        void UpdateSnapshot(IngestedAuditMessage message, AuditMessageSnapshot snapshot)
        {
            snapshot.AttemptedAt = GuessProcessingAttemptTime(message);
            EnrichWithBodyInformation(message, new SnapshotMetadata(snapshot.MessageMetadata));
            snapshotUpdater.Update(snapshot, message.Headers);
        }

        void EnrichWithBodyInformation(IngestedAuditMessage message, SnapshotMetadata metadata)
        {
            const int MaxBodySizeToStore = 1024 * 100; //100 kb

            metadata.Set("ContentLength", message.BodyLength);
            if (!message.HasBody)
            {
                return;
            }

            string contentType;

            if (!message.Headers.TryGet(Headers.ContentType, out contentType))
            {
                contentType = "text/xml"; //default to xml for now
            }

            metadata.Set("ContentType", contentType);

            var bodySize = message.BodyLength;
            var bodyId = message.Id;
            var bodyUrl = string.Format("/messages/{0}/body", bodyId);
            metadata.Set("BodyUrl", bodyUrl);

            if (!contentType.Contains("binary") && bodySize <= MaxBodySizeToStore)
            {
                metadata.Set("Body", System.Text.Encoding.UTF8.GetString(message.Body));
            }
        }

        static DateTime GuessProcessingAttemptTime(IngestedAuditMessage message)
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