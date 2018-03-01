namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ArchivingManager
    {
        IDomainEvents domainEvents;

        internal static Dictionary<string, InMemoryArchive> ArchiveOperations = new Dictionary<string, InMemoryArchive>();

        public ArchivingManager(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public bool IsArchiveInProgressFor(string requestId)
        {
            return ArchiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
        }

        internal IEnumerable<InMemoryArchive> GetArchivalOperations()
        {
            return ArchiveOperations.Values;
        }

        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            InMemoryArchive summary;
            if (!ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out summary))
            {
                return false;
            }

            return summary.ArchiveState != ArchiveState.ArchiveCompleted;
        }

        private InMemoryArchive GetOrCreate(ArchiveType archiveType, string requestId)
        {
            InMemoryArchive summary;
            if (!ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out summary))
            {
                summary = new InMemoryArchive(requestId, archiveType, domainEvents);
                ArchiveOperations[InMemoryArchive.MakeId(requestId, archiveType)] = summary;
            }
            return summary;
        }

        public void StartArchiving(ArchiveOperation archiveOperation)
        {
            var summary = GetOrCreate(archiveOperation.ArchiveType, archiveOperation.RequestId);

            summary.TotalNumberOfMessages = archiveOperation.TotalNumberOfMessages;
            summary.NumberOfMessagesArchived = archiveOperation.NumberOfMessagesArchived;
            summary.Started = archiveOperation.Started;
            summary.GroupName = archiveOperation.GroupName;
            summary.NumberOfBatches = archiveOperation.NumberOfBatches;
            summary.CurrentBatch = archiveOperation.CurrentBatch;

            summary.Start();
        }

        public void StartArchiving(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.TotalNumberOfMessages = 0;
            summary.NumberOfMessagesArchived = 0;
            summary.Started = DateTime.Now;
            summary.GroupName = "Undefined";
            summary.NumberOfBatches = 0;
            summary.CurrentBatch = 0;

            summary.Start();
        }

        public InMemoryArchive GetStatusForArchiveOperation(string requestId, ArchiveType archiveType)
        {
            InMemoryArchive summary;
            ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out summary);

            return summary;
        }

        public void BatchArchived(string requestId, ArchiveType archiveType, int numberOfMessagesArchivedInBatch)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.BatchArchived(numberOfMessagesArchivedInBatch);
        }

        public void ArchiveOperationFinalizing(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            summary.FinalizeArchive();
        }

        public void ArchiveOperationCompleted(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            summary.Complete();
        }

        public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
        {
            RemoveArchiveOperation(requestId, archiveType);
        }

        void RemoveArchiveOperation(string requestId, ArchiveType archiveType)
        {
            ArchiveOperations.Remove(InMemoryArchive.MakeId(requestId, archiveType));
        }
    }
}