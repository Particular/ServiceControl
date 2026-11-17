namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the failed error imports under the key a live import of the same message derives, and never replaces one the target already holds. 
/// A body larger than the target keeps inline goes to body storage, under the body id the SQL import store uses for that key, before the rows are inserted. 
/// A second import of the same message, in the batch or already in the target from another document, is reported as a merge.
/// </summary>
sealed class FailedErrorImportsWriter(IMigrationSqlDialect migrationDialect, IBodyStoragePersistence storagePersistence, EFPersisterSettings persisterSettings) : MigrationCategoryWriter<FailedErrorImport>
{
    public override string CategoryId => MigrationCategoryIds.FailedErrorImports;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<FailedErrorImportEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.FailedErrorImports.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, FailedErrorImport Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<FailedErrorImportEntity>.For(dbContext);
        List<(string SourceId, FailedErrorImportEntity Row, MigrationBody Body)> rows = [];
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];
        List<(string SourceId, Guid UniqueMessageId, string Detail)> changes = [];

        foreach (var (row, import) in documents)
        {
            var column = import.Message?.Id is null ? nameof(FailedErrorImportEntity.MessageId)
                : import.Message.Headers is null ? nameof(FailedErrorImportEntity.HeadersJson)
                : null;

            if (column is not null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, $"FailedErrorImports.{column} is NOT NULL and the document has no message, or its message has no value for it"));
                continue;
            }

            var failedAt = row.Metadata.GetValueOrDefault(MigrationMetadataKeys.FailedAt) as DateTime?
                ?? throw new InvalidOperationException($"Row '{row.SourceId}' in category {CategoryId} carries no {MigrationMetadataKeys.FailedAt}, so the import's failure time is unknown.");
            var body = row.Body
                ?? throw new InvalidOperationException($"Row '{row.SourceId}' in category {CategoryId} reached the writer without a body. The engine attaches a body to every row of a category that carries bodies, or skips the row.");
            var entity = FailedErrorImportRowMapper.Map(import, failedAt, body.Content.Length > persisterSettings.BodyStorage.MaxBodySizeToStore);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add((row.SourceId, entity, body));

            if (EmptiedValues(import) is { } emptied)
            {
                changes.Add((row.SourceId, entity.UniqueMessageId, emptied));
            }
        }

        var stored = await FailureTimesStoredFor(dbContext, rows, cancellationToken);
        var (kept, merges) = KeptAndMerged(rows, stored);

        // Every import of one message shares one body id, so only the row the insert keeps may write its body.
        await WriteBodies([.. rows.Where(row => row.Row.BodyStoredExternally && !stored.ContainsKey(row.Row.UniqueMessageId) && kept[row.Row.UniqueMessageId] == row.SourceId)], cancellationToken);

        return new PreparedBatch(async (context, token) => (await migrationDialect.InsertMissing(context, [.. rows.Select(row => row.Row)], token)).Count, rows.Count, skips, merges)
        {
            Changes = [.. changes.Where(change => !stored.ContainsKey(change.UniqueMessageId) && kept[change.UniqueMessageId] == change.SourceId).Select(change => (change.SourceId, change.Detail))]
        };
    }

    // FailedErrorImportRowMapper stores a missing exception or body as empty, because both columns are NOT NULL.
    static string? EmptiedValues(FailedErrorImport import) =>
        (import.ExceptionInfo, import.Message!.Body) switch
        {
            (null, null) => "Stored an empty exception and an empty body, because the document has neither.",
            (null, _) => "Stored an empty exception, because the document has none.",
            (_, null) => "Stored an empty body, because the document's message has none.",
            _ => null
        };

    static async Task<Dictionary<Guid, DateTime>> FailureTimesStoredFor(ServiceControlDbContext dbContext, IReadOnlyList<(string SourceId, FailedErrorImportEntity Row, MigrationBody Body)> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.Row.UniqueMessageId).Distinct().ToArray();

        return await dbContext.FailedErrorImports
            .AsNoTracking()
            .Where(import => ids.Contains(import.UniqueMessageId))
            .ToDictionaryAsync(import => import.UniqueMessageId, import => import.FailedAt, cancellationToken);
    }

    // A stored row wins over every row of the batch, and an earlier row over a later one, as in the insert. A stored row with the same failure time is this document's own row, which a retry reads again.
    static (Dictionary<Guid, string?> Kept, List<(string SourceId, string Detail)> Merges) KeptAndMerged(IReadOnlyList<(string SourceId, FailedErrorImportEntity Row, MigrationBody Body)> rows, Dictionary<Guid, DateTime> stored)
    {
        var kept = stored.ToDictionary(entry => entry.Key, entry => (string?)null);
        List<(string SourceId, string Detail)> merges = [];

        foreach (var (sourceId, row, _) in rows)
        {
            if (!kept.TryGetValue(row.UniqueMessageId, out var keeper) || (keeper is null && SameFailureTime(stored[row.UniqueMessageId], row.FailedAt)))
            {
                kept[row.UniqueMessageId] = sourceId;
            }
            else if (keeper is null)
            {
                merges.Add((sourceId, $"FailedErrorImports already holds an import of the same message, '{row.MessageId}', that failed at {stored[row.UniqueMessageId]:O}, while this document failed at {row.FailedAt:O}, so it kept the stored row and dropped this one"));
            }
            else
            {
                merges.Add((sourceId, $"'{keeper}' is an import of the same message, '{row.MessageId}', so FailedErrorImports kept the row from '{keeper}' and dropped the row from '{sourceId}'"));
            }
        }

        return (kept, merges);
    }

    // PostgreSQL keeps only whole microseconds, so the stored time of the same document can differ from a re-read by less than one.
    static bool SameFailureTime(DateTime stored, DateTime read) => Math.Abs((stored - read).Ticks) < TimeSpan.TicksPerMicrosecond;

    // All bodies are written before the transaction opens, so a crash leaves a body with no row rather than a row pointing at a body that was never stored.
    Task WriteBodies(IReadOnlyList<(string SourceId, FailedErrorImportEntity Row, MigrationBody Body)> externalBodies, CancellationToken cancellationToken) =>
        Task.WhenAll(externalBodies.Select(external => WriteBody(external, cancellationToken)));

    async Task WriteBody((string SourceId, FailedErrorImportEntity Row, MigrationBody Body) external, CancellationToken cancellationToken)
    {
        var bodyId = FailedErrorImportEntity.ExternalBodyId(external.Row.UniqueMessageId);

        try
        {
            await storagePersistence.WriteBody(bodyId, external.Body.Content, external.Body.ContentType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // The storage SDK errors do not name the body, so without this the error that stops the copy would not say which import failed.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Writing the body of document '{external.SourceId}' in category {CategoryId} to external body storage as '{bodyId}' ({external.Body.Content.Length} bytes) failed with {ex.GetType().Name}: {ex.Message}", ex);
        }
    }
}
