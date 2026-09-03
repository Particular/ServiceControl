namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.Archiving.Metrics;

/// <summary>
/// EFCore equivalent of the RavenDB <see cref="UnarchivingManager"/>. Wraps the shared
/// <see cref="OperationsManager"/> singleton to manage in-memory unarchive progress state.
/// </summary>
class EFCoreUnarchivingManager(IDomainEvents domainEvents, OperationsManager operationsManager, ArchiveMetrics metrics, TimeProvider timeProvider)
{
    InMemoryUnarchive GetOrCreate(ArchiveType archiveType, string requestId)
    {
        var id = InMemoryUnarchive.MakeId(requestId, archiveType);
        if (!operationsManager.UnarchiveOperations.TryGetValue(id, out var summary))
        {
            summary = new InMemoryUnarchive(requestId, archiveType, domainEvents, timeProvider, metrics);
            operationsManager.UnarchiveOperations[id] = summary;
        }

        return summary;
    }

    public Task StartUnarchiving(ArchiveOperationEntity operation, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(operation.ArchiveType, operation.RequestId);

        summary.TotalNumberOfMessages = operation.TotalNumberOfMessages;
        summary.NumberOfMessagesUnarchived = operation.NumberOfMessagesProcessed;
        summary.Started = operation.Started;
        summary.GroupName = operation.GroupName;
        summary.NumberOfBatches = operation.NumberOfBatches;
        summary.CurrentBatch = operation.CurrentBatch;

        return summary.Start(cancellationToken);
    }

    public Task StartUnarchiving(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(archiveType, requestId);

        summary.TotalNumberOfMessages = 0;
        summary.NumberOfMessagesUnarchived = 0;
        summary.Started = timeProvider.GetUtcNow().UtcDateTime;
        summary.GroupName = "Undefined";
        summary.NumberOfBatches = 0;
        summary.CurrentBatch = 0;

        return summary.Start(cancellationToken);
    }

    public Task BatchUnarchived(string requestId, ArchiveType archiveType, int numberOfMessagesUnarchivedInBatch, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(archiveType, requestId);
        return summary.BatchUnarchived(numberOfMessagesUnarchivedInBatch, cancellationToken);
    }

    public Task UnarchiveOperationFinalizing(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(archiveType, requestId);
        return summary.FinalizeUnarchive(cancellationToken);
    }

    public Task UnarchiveOperationCompleted(string requestId, ArchiveType archiveType, CancellationToken cancellationToken = default)
    {
        var summary = GetOrCreate(archiveType, requestId);
        return summary.Complete(cancellationToken);
    }
}