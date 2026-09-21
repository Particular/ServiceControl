namespace ServiceControl.Persistence.EFCore.DataMigration;

using System.Collections.Frozen;
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
sealed class EFCoreMigrationTarget(IServiceScopeFactory scopeFactory, IMigrationSqlDialect migrationDialect, ILogger<EFCoreMigrationTarget> logger) : DataStoreBase(scopeFactory), IMigrationTarget
{
    readonly FrozenDictionary<string, IMigrationCategoryWriter> writers = new IMigrationCategoryWriter[]
    {
        new KnownEndpointsWriter(scopeFactory, migrationDialect),
        new EndpointSettingsWriter(scopeFactory, migrationDialect)
    }.ToFrozenDictionary(writer => writer.CategoryId, StringComparer.Ordinal);

    public Task Open(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => migrationDialect.Open(dbContext, token), cancellationToken);

    public int BatchSizeFor(MigrationCategory category) => WriterFor(category).BatchSize;

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

            return new MigrationWriteResult(
                saved,
                copied,
                prepared.Skips.Count,
                [.. prepared.Skips.Select(skip => skip.SourceId)],
                AlreadyPresent: alreadyPresent,
                SkipReasons: skipReasons,
                BenignSkipped: prepared.BenignSkipCount);
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

    public IReadOnlyCollection<string> SupportedCategoryIds => writers.Keys;

    IMigrationCategoryWriter WriterFor(MigrationCategory category) =>
        writers.TryGetValue(category.Id, out var writer)
            ? writer
            : throw new NotSupportedException($"The migration target cannot yet write the '{category.Id}' category.");
}
