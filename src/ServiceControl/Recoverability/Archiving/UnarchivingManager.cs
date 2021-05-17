﻿namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    class UnarchivingManager
    {
        public UnarchivingManager(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public bool IsUnarchiveInProgressFor(string requestId)
        {
            return unarchiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
        }

        internal IEnumerable<InMemoryUnarchive> GetUnarchivalOperations()
        {
            return unarchiveOperations.Values;
        }

        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            if (!unarchiveOperations.TryGetValue(InMemoryUnarchive.MakeId(requestId, archiveType), out var summary))
            {
                return false;
            }

            return summary.ArchiveState != ArchiveState.ArchiveCompleted;
        }

        InMemoryUnarchive GetOrCreate(ArchiveType archiveType, string requestId)
        {
            if (!unarchiveOperations.TryGetValue(InMemoryUnarchive.MakeId(requestId, archiveType), out var summary))
            {
                summary = new InMemoryUnarchive(requestId, archiveType, domainEvents);
                unarchiveOperations[InMemoryUnarchive.MakeId(requestId, archiveType)] = summary;
            }

            return summary;
        }

        public Task StartUnarchiving(UnarchiveOperation archiveOperation)
        {
            var summary = GetOrCreate(archiveOperation.ArchiveType, archiveOperation.RequestId);

            summary.TotalNumberOfMessages = archiveOperation.TotalNumberOfMessages;
            summary.NumberOfMessagesUnarchived = archiveOperation.NumberOfMessagesUnarchived;
            summary.Started = archiveOperation.Started;
            summary.GroupName = archiveOperation.GroupName;
            summary.NumberOfBatches = archiveOperation.NumberOfBatches;
            summary.CurrentBatch = archiveOperation.CurrentBatch;

            return summary.Start();
        }

        public Task StartUnarchiving(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.TotalNumberOfMessages = 0;
            summary.NumberOfMessagesUnarchived = 0;
            summary.Started = DateTime.Now;
            summary.GroupName = "Undefined";
            summary.NumberOfBatches = 0;
            summary.CurrentBatch = 0;

            return summary.Start();
        }

        public InMemoryUnarchive GetStatusForUnarchiveOperation(string requestId, ArchiveType archiveType)
        {
            unarchiveOperations.TryGetValue(InMemoryUnarchive.MakeId(requestId, archiveType), out var summary);

            return summary;
        }

        public Task BatchUnarchived(string requestId, ArchiveType archiveType, int numberOfMessagesArchivedInBatch)
        {
            var summary = GetOrCreate(archiveType, requestId);

            return summary.BatchUnarchived(numberOfMessagesArchivedInBatch);
        }

        public Task UnarchiveOperationFinalizing(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.FinalizeUnarchive();
        }

        public Task UnarchiveOperationCompleted(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.Complete();
        }

        public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
        {
            RemoveUnarchiveOperation(requestId, archiveType);
        }

        void RemoveUnarchiveOperation(string requestId, ArchiveType archiveType)
        {
            unarchiveOperations.Remove(InMemoryUnarchive.MakeId(requestId, archiveType));
        }

        IDomainEvents domainEvents;

        Dictionary<string, InMemoryUnarchive> unarchiveOperations = new Dictionary<string, InMemoryUnarchive>();
    }
}