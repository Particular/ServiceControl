namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        var historicText = HistoricTextColumns(dbContext);
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
                else if (UnstorableHistoricValue(historicText, row, RetryOperationRowMapper.Map(historicOperation)) is { } unstorable)
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

                // Fresh entities per attempt, because a failed save can leave generated ids on the previous ones.
                // Reversed, so on a tie in completion time the first in the document still wins, as in RavenDB.
                context.HistoricRetryOperations.AddRange(Enumerable.Reverse(historic).Select(RetryOperationRowMapper.Map));
                await context.SaveChangesAsync(token);

                return historic.Count + inserted.Count;
            },
            historic.Count + unacknowledged.Count,
            skips,
            await KeyMerges.In(dbContext, migrationDialect, unacknowledged, cancellationToken));
    }

    // Read from the model, because MigrationInsert<T>.For refuses a table whose key the store generates.
    static (IProperty Property, string TableAndColumn)[] HistoricTextColumns(ServiceControlDbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(HistoricRetryOperationEntity))!;

        return [.. entityType.GetProperties().Where(property => property.ClrType == typeof(string)).Select(property => (property, $"{entityType.GetTableName()}.{property.GetColumnName()}"))];
    }

    (string SourceId, MigrationSkipReason Reason, string Detail)? UnstorableHistoricValue((IProperty Property, string TableAndColumn)[] text, MigrationRow row, HistoricRetryOperationEntity entity)
    {
        foreach (var (property, tableAndColumn) in text)
        {
            if (property.GetGetter().GetClrValue(entity) is string { Length: var units } value && property.GetMaxLength() is { } maxLength && units > maxLength && migrationDialect.TextLength(value) is var length && length > maxLength)
            {
                return (row.SourceId, MigrationSkipReason.ValueTooLong, $"{tableAndColumn} takes at most {maxLength} characters and the value holds {length}, on document '{row.SourceId}'");
            }
        }

        foreach (var (property, tableAndColumn) in text)
        {
            if (property.GetGetter().GetClrValue(entity) is string value && migrationDialect.RejectedCharacterIn(value) is { } rejected)
            {
                return (row.SourceId, MigrationSkipReason.MalformedValue, $"{tableAndColumn} cannot hold the character U+{(int)rejected:X4}, which the value contains, on document '{row.SourceId}'");
            }
        }

        return null;
    }
}
