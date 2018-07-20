namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    public class ArchivingManager
    {
        public ArchivingManager(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public bool IsArchiveInProgressFor(string requestId)
        {
            return archiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
        }

        internal IEnumerable<InMemoryArchive> GetArchivalOperations()
        {
            return archiveOperations.Values;
        }

        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            if (!archiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var summary))
            {
                return false;
            }

            return summary.ArchiveState != ArchiveState.ArchiveCompleted;
        }

        InMemoryArchive GetOrCreate(ArchiveType archiveType, string requestId)
        {
            if (!archiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var summary))
            {
                summary = new InMemoryArchive(requestId, archiveType, domainEvents);
                archiveOperations[InMemoryArchive.MakeId(requestId, archiveType)] = summary;
            }
            return summary;
        }

        public Task StartArchiving(ArchiveOperation archiveOperation)
        {
            var summary = GetOrCreate(archiveOperation.ArchiveType, archiveOperation.RequestId);

            summary.TotalNumberOfMessages = archiveOperation.TotalNumberOfMessages;
            summary.NumberOfMessagesArchived = archiveOperation.NumberOfMessagesArchived;
            summary.Started = archiveOperation.Started;
            summary.GroupName = archiveOperation.GroupName;
            summary.NumberOfBatches = archiveOperation.NumberOfBatches;
            summary.CurrentBatch = archiveOperation.CurrentBatch;

            return summary.Start();
        }

        public Task StartArchiving(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.TotalNumberOfMessages = 0;
            summary.NumberOfMessagesArchived = 0;
            summary.Started = DateTime.Now;
            summary.GroupName = "Undefined";
            summary.NumberOfBatches = 0;
            summary.CurrentBatch = 0;

            return summary.Start();
        }

        public InMemoryArchive GetStatusForArchiveOperation(string requestId, ArchiveType archiveType)
        {
            archiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var summary);

            return summary;
        }

        public Task BatchArchived(string requestId, ArchiveType archiveType, int numberOfMessagesArchivedInBatch)
        {
            var summary = GetOrCreate(archiveType, requestId);

            return summary.BatchArchived(numberOfMessagesArchivedInBatch);
        }

        public Task ArchiveOperationFinalizing(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.FinalizeArchive();
        }

        public Task ArchiveOperationCompleted(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.Complete();
        }

        public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
        {
            RemoveArchiveOperation(requestId, archiveType);
        }

        void RemoveArchiveOperation(string requestId, ArchiveType archiveType)
        {
            archiveOperations.Remove(InMemoryArchive.MakeId(requestId, archiveType));
        }

        IDomainEvents domainEvents;

        Dictionary<string, InMemoryArchive> archiveOperations = new Dictionary<string, InMemoryArchive>();
    }
}