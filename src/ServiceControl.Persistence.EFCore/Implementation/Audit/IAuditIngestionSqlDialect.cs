namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// The provider-specific SQL of the audit ingestion batch. Both statements are plain multi-row
/// inserts with no conflict clause: audit rows are never deduplicated, so a competing writer can
/// never collide with them. Implementations run on the DbContext connection inside the transaction
/// the caller has already opened.
/// </summary>
public interface IAuditIngestionSqlDialect
{
    Task InsertAuditMessages(ServiceControlDbContext dbContext, IReadOnlyList<AuditMessageEntity> rows, CancellationToken cancellationToken = default);

    Task InsertSagaSnapshots(ServiceControlDbContext dbContext, IReadOnlyList<SagaSnapshotEntity> rows, CancellationToken cancellationToken = default);
}
