namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

// Writes the audit half of an ingestion batch inside the transaction the unit of work has opened,
// after the failed message writer so that a batch's audit rows commit together with the known
// endpoints it recorded.
class AuditBatchWriter(ServiceControlDbContext dbContext, IAuditIngestionSqlDialect dialect)
{
    public async Task Write(IReadOnlyCollection<AuditMessageEntity> messages, IReadOnlyCollection<SagaSnapshotEntity> snapshots, CancellationToken cancellationToken = default)
    {
        if (messages.Count > 0)
        {
            await dialect.InsertAuditMessages(dbContext, [.. messages], cancellationToken);
        }

        if (snapshots.Count > 0)
        {
            await dialect.InsertSagaSnapshots(dbContext, [.. snapshots], cancellationToken);
        }
    }
}
