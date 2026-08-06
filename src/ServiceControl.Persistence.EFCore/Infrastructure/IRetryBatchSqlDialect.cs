namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// The provider-specific SQL used to claim messages for retry batches.
/// </summary>
public interface IRetryBatchSqlDialect
{
    /// <summary>
    /// Insert if absent, never update: a message already claimed stays with the batch that claimed
    /// it first, so two retry requests covering the same message cannot both stage it.
    /// </summary>
    Task InsertMissingRetryClaims(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageRetryEntity> rows, CancellationToken cancellationToken);
}
