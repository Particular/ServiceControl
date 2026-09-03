namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    class ArchivingManager
    {
        public ArchivingManager(IDomainEvents domainEvents, OperationsManager operationsManager, TimeProvider timeProvider)
        {
            this.domainEvents = domainEvents;
            this.operationsManager = operationsManager;
            this.timeProvider = timeProvider;
        }

        public bool IsArchiveInProgressFor(string requestId)
        {
            return operationsManager.ArchiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
        }

        internal IEnumerable<InMemoryArchive> GetArchivalOperations()
        {
            return operationsManager.ArchiveOperations.Values;
        }

        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            return operationsManager.IsOperationInProgressFor(requestId, archiveType);
        }

        InMemoryArchive GetOrCreate(ArchiveType archiveType, string requestId)
        {
            if (!operationsManager.ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var summary))
            {
                summary = new InMemoryArchive(requestId, archiveType, domainEvents, timeProvider);
                operationsManager.ArchiveOperations[InMemoryArchive.MakeId(requestId, archiveType)] = summary;
            }

            return summary;
        }

        public Task StartArchiving(ArchiveOperation archiveOperation, CancellationToken cancellationToken = default)
        {
            var summary = GetOrCreate(archiveOperation.ArchiveType, archiveOperation.RequestId);

            summary.TotalNumberOfMessages = archiveOperation.TotalNumberOfMessages;
            summary.NumberOfMessagesArchived = archiveOperation.NumberOfMessagesArchived;
            summary.Started = archiveOperation.Started;
            summary.GroupName = archiveOperation.GroupName;
            summary.NumberOfBatches = archiveOperation.NumberOfBatches;
            summary.CurrentBatch = archiveOperation.CurrentBatch;

            return summary.Start(cancellationToken);
        }

        public Task StartArchiving(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.TotalNumberOfMessages = 0;
            summary.NumberOfMessagesArchived = 0;
            summary.Started = timeProvider.GetUtcNow().UtcDateTime;
            summary.GroupName = "Undefined";
            summary.NumberOfBatches = 0;
            summary.CurrentBatch = 0;

            return summary.Start(cancellationToken);
        }

        public InMemoryArchive GetStatusForArchiveOperation(string requestId, ArchiveType archiveType)
        {
            operationsManager.ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var summary);

            return summary;
        }

        public Task BatchArchived(string requestId, ArchiveType archiveType, int numberOfMessagesArchivedInBatch, CancellationToken cancellationToken = default)
        {
            var summary = GetOrCreate(archiveType, requestId);

            return summary.BatchArchived(numberOfMessagesArchivedInBatch, cancellationToken);
        }

        public Task ArchiveOperationFinalizing(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.FinalizeArchive(cancellationToken);
        }

        public Task ArchiveOperationCompleted(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
        {
            var summary = GetOrCreate(archiveType, requestId);
            return summary.Complete(cancellationToken);
        }

        public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
        {
            RemoveArchiveOperation(requestId, archiveType);
        }

        void RemoveArchiveOperation(string requestId, ArchiveType archiveType)
        {
            operationsManager.ArchiveOperations.Remove(InMemoryArchive.MakeId(requestId, archiveType));
        }

        IDomainEvents domainEvents;
        readonly TimeProvider timeProvider;

        OperationsManager operationsManager;

    }
}