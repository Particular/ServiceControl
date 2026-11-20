namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Recoverability;

/// <summary>
/// Copies the retry history into two tables. A pending operation whose key is already taken is reported as a merge.
/// Historic operations get new keys, so a retry's reset empties both tables before the history is read again.
/// An operation that cannot be stored is skipped with a reason, and the rest of the batch still copies.
/// </summary>
sealed class RetryOperationsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<RetryOperationRow>, IStoreKeyedCategoryWriter
{
    public override string CategoryId => MigrationCategoryIds.RetryOperations;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<UnacknowledgedRetryOperationEntity>(dbContext);

    public override async Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        await dbContext.HistoricRetryOperations.LongCountAsync(cancellationToken) + await dbContext.UnacknowledgedRetryOperations.LongCountAsync(cancellationToken);

    // Whole tables, because the target is refused unless it held no ServiceControl data before the first copy.
    public async Task<int> DeleteCopiedRows(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        await dbContext.HistoricRetryOperations.ExecuteDeleteAsync(cancellationToken) + await dbContext.UnacknowledgedRetryOperations.ExecuteDeleteAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, RetryOperationRow Document)> documents, CancellationToken cancellationToken = default)
    {
        var pendingInsert = MigrationInsert<UnacknowledgedRetryOperationEntity>.For(dbContext);
        var historicText = StoreKeyedInsert.TextColumns<HistoricRetryOperationEntity>(dbContext);
        List<HistoricRetryOperation> historic = [];
        List<(string SourceId, UnacknowledgedRetryOperationEntity Row)> unacknowledged = [];
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];

        foreach (var (row, operation) in documents)
        {
            if (operation.Historic is { } historicOperation)
            {
                if (historicOperation.RequestId is null)
                {
                    skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, "HistoricRetryOperations.RequestId is NOT NULL and the document has no value for it"));
                }
                else if (StoreKeyedInsert.UnstorableValueSkip(historicText, migrationDialect, row, RetryOperationRowMapper.Map(historicOperation)) is { } unstorable)
                {
                    skips.Add(unstorable);
                }
                else
                {
                    historic.Add(historicOperation);
                }

                continue;
            }

            var pending = operation.Unacknowledged!;

            if (pending.RequestId is null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, "UnacknowledgedRetryOperations.RequestId is NOT NULL and the document has no value for it"));
                continue;
            }

            var entity = RetryOperationRowMapper.Map(pending);

            if (UnstorableValueSkip(pendingInsert, migrationDialect, row, entity) is { } unstorablePending)
            {
                skips.Add(unstorablePending);
                continue;
            }

            unacknowledged.Add((row.SourceId, entity));
        }

        // Reversed, because the insert keeps the first of a repeated key and the latest retry must win.
        unacknowledged.Reverse();

        return new PreparedBatch(
            async (context, token) =>
            {
                var inserted = await migrationDialect.InsertMissing(context, [.. unacknowledged.Select(pending => pending.Row)], token);

                // Reversed so the first in the document is added last and gets the highest id, which wins a tie on completion time.
                // That holds only while the store hands out ids in add order.
                await StoreKeyedInsert.AddAll(context, [.. Enumerable.Reverse(historic)], (HistoricRetryOperation operation) => RetryOperationRowMapper.Map(operation), token);

                return historic.Count + inserted.Count;
            },
            historic.Count + unacknowledged.Count,
            skips,
            await KeyMerges.In(dbContext, migrationDialect, unacknowledged, cancellationToken))
        {
            RenumberedRows = historic.Count == 0 ? null : "historic retry operations"
        };
    }
}
