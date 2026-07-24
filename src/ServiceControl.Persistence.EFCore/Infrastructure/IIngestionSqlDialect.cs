namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// The provider specific SQL of the error ingestion batch. Implementations run on the DbContext
/// connection inside the transaction the caller has already opened, and every statement must stay
/// correct under concurrent writers: a same-key race between two instances may not fail the batch.
/// </summary>
public interface IIngestionSqlDialect
{
    /// <summary>
    /// One row per message, distinct by UniqueMessageId. Inserts new rows; for existing rows the
    /// attempt counter advances (unless the batch merely redelivered the attempt already stored),
    /// the failure window widens, and the newer attempt supplies every payload column.
    /// </summary>
    Task UpsertFailedMessages(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageEntity> rows, CancellationToken cancellationToken);

    /// <summary>
    /// Insert if absent. The caller has already deleted the batch's messages' group rows in the
    /// same transaction; if-absent keeps a concurrent writer's identical row from failing us.
    /// </summary>
    Task InsertGroups(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageGroupEntity> rows, CancellationToken cancellationToken);

    /// <summary>
    /// Insert if absent, never update: existing endpoints keep their Monitored flag.
    /// </summary>
    Task InsertMissingKnownEndpoints(ServiceControlDbContext dbContext, IReadOnlyList<KnownEndpointEntity> rows, CancellationToken cancellationToken);
}
