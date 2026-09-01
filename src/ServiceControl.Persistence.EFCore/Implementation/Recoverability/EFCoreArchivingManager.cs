namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.Archiving.Metrics;

/// <summary>
/// EFCore equivalent of the RavenDB <see cref="ArchivingManager"/>. Wraps the shared
/// <see cref="OperationsManager"/> singleton to manage in-memory archive progress state.
/// </summary>
class EFCoreArchivingManager(IDomainEvents domainEvents, OperationsManager operationsManager, ArchiveMetrics metrics)
{
    InMemoryArchive GetOrCreate(ArchiveType archiveType, string requestId)
    {
        var id = InMemoryArchive.MakeId(requestId, archiveType);
        if (!operationsManager.ArchiveOperations.TryGetValue(id, out var summary))
        {
            summary = new InMemoryArchive(requestId, archiveType, domainEvents, metrics);
            operationsManager.ArchiveOperations[id] = summary;
        }

        return summary;
    }

    public Task StartArchiving(ArchiveOperationEntity operation, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(operation.ArchiveType, operation.RequestId);

        summary.TotalNumberOfMessages = operation.TotalNumberOfMessages;
        summary.NumberOfMessagesArchived = operation.NumberOfMessagesProcessed;
        summary.Started = operation.Started;
        summary.GroupName = operation.GroupName;
        summary.NumberOfBatches = operation.NumberOfBatches;
        summary.CurrentBatch = operation.CurrentBatch;

        return summary.Start(cancellationToken);
    }

    public Task StartArchiving(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(archiveType, requestId);

        summary.TotalNumberOfMessages = 0;
        summary.NumberOfMessagesArchived = 0;
        summary.Started = DateTime.UtcNow;
        summary.GroupName = "Undefined";
        summary.NumberOfBatches = 0;
        summary.CurrentBatch = 0;

        return summary.Start(cancellationToken);
    }

    public InMemoryArchive? GetStatusForArchiveOperation(string requestId, ArchiveType archiveType)
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

    public bool IsArchiveInProgressFor(string requestId)
    {
        return operationsManager.ArchiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
    }

    public IEnumerable<InMemoryArchive> GetArchivalOperations()
    {
        return operationsManager.ArchiveOperations.Values;
    }

    public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
    {
        operationsManager.ArchiveOperations.Remove(InMemoryArchive.MakeId(requestId, archiveType));
    }
}