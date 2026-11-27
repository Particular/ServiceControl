namespace ServiceControl.Persistence.EFCore.DataMigration;

using System.Collections.Frozen;
using Abstractions;
using Implementation;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.DataMigration;
using Writers;

/// <summary>
/// Writes a migration into SQL Server or PostgreSQL. It holds one writer per category and knows nothing about
/// any of them beyond that, so a category this build cannot write is simply absent from
/// <see cref="SupportedCategoryIds" /> and never reaches the engine.
/// </summary>
sealed class EFCoreMigrationTarget(
    IServiceScopeFactory scopeFactory,
    IMigrationSqlDialect migrationDialect,
    IBodyStoragePersistence storagePersistence,
    EFPersisterSettings settings,
    ILogger<EFCoreMigrationTarget> logger) : DataStoreBase(scopeFactory), IMigrationTarget
{
    readonly FrozenDictionary<string, IMigrationCategoryWriter> writers = new IMigrationCategoryWriter[]
    {
        new KnownEndpointsWriter(migrationDialect),
        new EndpointSettingsWriter(migrationDialect),
        new FailedMessagesWriter(migrationDialect, storagePersistence, settings),
        new UnresolvedFailedMessagesWriter(migrationDialect, storagePersistence, settings),
        new LicensingEndpointsWriter(migrationDialect),
        new LicensingThroughputWriter(migrationDialect),
        new LicensingReportMasksWriter(migrationDialect),
        new LicensedEndpointDetailsWriter(migrationDialect),
        new MessageRedirectsWriter(migrationDialect),
        new SubscriptionsWriter(migrationDialect),
        new NotificationSettingsWriter(migrationDialect),
        new TrialEndDateWriter(migrationDialect),
        new RetryOperationsWriter(migrationDialect),
        new PendingIntegrationEventsWriter(migrationDialect),
        new CustomChecksWriter(migrationDialect),
        new FailedErrorImportsWriter(migrationDialect, storagePersistence, settings),
        new GroupCommentsWriter(migrationDialect)
    }.ToFrozenDictionary(writer => writer.CategoryId, StringComparer.Ordinal);

    public Task Open(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => migrationDialect.Open(dbContext, token), cancellationToken);

    public Task<int> BatchSizeFor(MigrationCategory category, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, _) => Task.FromResult(WriterFor(category).BatchSize(dbContext)), cancellationToken);

    public Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointToExtend,
        CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, writeToken) =>
        {
            var prepared = await WriterFor(category).Prepare(dbContext, batch, writeToken);

            AccountForEveryRow(category, batch, prepared);

            var skipReasons = prepared.Skips.Count == 0 ? null : prepared.Skips.GroupBy(skip => skip.Reason).ToDictionary(group => group.Key, group => group.LongCount());

            var (copied, alreadyPresent, saved) = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                // This block is retried, and an attempt that failed leaves the checkpoint it added still tracked, so the next attempt would insert it a second time.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

                var inserted = await prepared.Insert(dbContext, token);
                var present = AlreadyPresentIn(category, batch, inserted, prepared.Skips.Count);
                var stored = await dbContext.UpsertCheckpoint(
                    checkpointToExtend.Extend(inserted, prepared.Skips.Count, present, skipReasons),
                    token);
                await transaction.CommitAsync(token);

                return (inserted, present, stored);
            }, writeToken);

            foreach (var (sourceId, reason, detail) in prepared.Skips)
            {
                logger.LogWarning("Skipped {SourceId} in category {CategoryId} as {SkipReason}: {Detail}", sourceId, category.Id, reason, detail);
            }

            foreach (var (sourceId, detail) in prepared.Merges)
            {
                logger.LogWarning("Merged {SourceId} in category {CategoryId} into a row with the same key: {Detail}", sourceId, category.Id, detail);
            }

            foreach (var (sourceId, detail) in prepared.Changes)
            {
                logger.LogWarning("Changed {SourceId} in category {CategoryId} on the way into SQL: {Detail}", sourceId, category.Id, detail);
            }

            // Once per copy of the category, because the line describes every batch alike and a retry's reset clears the count.
            if (prepared.RenumberedRows is { } renumbered && checkpointToExtend.CopiedCount == 0)
            {
                logger.LogInformation("Category {CategoryId}: the database gives the copied {RenumberedRows} new ids. Nothing in SQL refers to the ids they had in the source.", category.Id, renumbered);
            }

            return new MigrationWriteResult(
                saved,
                copied,
                prepared.Skips.Count,
                [.. prepared.Skips.Select(skip => skip.SourceId)],
                AlreadyPresent: alreadyPresent,
                SkipReasons: skipReasons,
                Merged: prepared.Merges.Count,
                Changed: prepared.Changes.Count);
        }, cancellationToken);

    // Checked before the insert, because the subtraction below can only catch a writer that over-reports.
    internal static void AccountForEveryRow(MigrationCategory category, MigrationBatch batch, PreparedBatch prepared)
    {
        if (prepared.PreparedRowCount + prepared.Skips.Count != batch.Rows.Count)
        {
            throw new InvalidOperationException($"The {category.Id} writer prepared {prepared.PreparedRowCount} rows and skipped {prepared.Skips.Count} of the {batch.Rows.Count} rows in the batch. Every row must be one or the other, or the rows it dropped would be counted as rows the target already held.");
        }
    }

    // Without the throw, a miscount would quietly shrink the halt threshold's denominator instead of failing.
    internal static int AlreadyPresentIn(MigrationCategory category, MigrationBatch batch, int copied, int skipped)
    {
        var alreadyPresent = batch.Rows.Count - copied - skipped;

        if (alreadyPresent < 0)
        {
            throw new InvalidOperationException($"The target copied {copied} and skipped {skipped} of the {batch.Rows.Count} rows in category {category.Id}, leaving {alreadyPresent} already present. Every row is copied, skipped or already present.");
        }

        return alreadyPresent;
    }

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => WriterFor(category).Count(dbContext, token), cancellationToken);

    public Task<MigrationCheckpoint> ResetForRetry(MigrationCheckpoint reset, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, resetToken) =>
        {
            var storeKeyed = writers.GetValueOrDefault(reset.CategoryId) as IStoreKeyedCategoryWriter;

            var (deleted, saved) = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

                // Only a reset that deletes is refused, because an optional category is retried after ServiceControl opens and keeps its rows.
                if (storeKeyed is not null && await dbContext.GetSetting<DateTime?>(SettingKeys.MigrationHostOpenedOnTarget, token) is not null)
                {
                    throw new InvalidOperationException($"Category {reset.CategoryId} cannot be retried, because ServiceControl has opened on this database since the copy and its tables may now hold live rows. A retry deletes every row in them, so it is only possible before ServiceControl first opens on this database.");
                }

                var deletedRows = storeKeyed is null ? 0 : await storeKeyed.DeleteCopiedRows(dbContext, token);
                var stored = await dbContext.UpsertCheckpoint(reset, token);
                await transaction.CommitAsync(token);

                return (deletedRows, stored);
            }, resetToken);

            if (storeKeyed is null)
            {
                logger.LogInformation("Reset category {CategoryId} for a retry and kept the rows it copied", reset.CategoryId);
            }
            else
            {
                logger.LogInformation("Reset category {CategoryId} for a retry and deleted the {DeletedRowCount} rows it copied, because a re-read would add them a second time", reset.CategoryId, deleted);
            }

            return saved;
        }, cancellationToken);

    public IReadOnlyCollection<string> SupportedCategoryIds => writers.Keys;

    public IReadOnlyDictionary<string, Type> DocumentTypes => writers.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.DocumentType, StringComparer.Ordinal);

    IMigrationCategoryWriter WriterFor(MigrationCategory category) =>
        writers.TryGetValue(category.Id, out var writer)
            ? writer
            : throw new NotSupportedException($"The migration target cannot yet write the '{category.Id}' category.");
}
