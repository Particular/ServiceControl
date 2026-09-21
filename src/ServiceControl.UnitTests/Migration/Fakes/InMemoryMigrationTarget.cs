namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class InMemoryMigrationTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
{
    readonly Dictionary<string, HashSet<string>> writtenKeysByCategory = [];
    readonly Dictionary<string, List<MigrationRow>> writtenRowsByCategory = [];
    readonly Dictionary<string, List<MigrationRow>> rowsHandedToWriteByCategory = [];
    readonly HashSet<string> preExistingKeys = [];
    readonly Dictionary<string, (MigrationSkipReason Reason, bool Benign)> rejectedKeys = [];

    public int DefaultBatchSize { get; set; } = 3;
    public string NoBatchSizeFor { get; set; }
    public int? FailOnCallNumber { get; set; }

    /// <summary>
    /// What FailOnCallNumber throws, when the default simulated failure is the wrong shape for the test.
    /// </summary>
    public Exception FailWith { get; set; }

    /// <summary>
    /// Runs at the start of every write, before any simulated failure, with the checkpoint the engine is extending, so a test can watch a stamp move between batches.
    /// </summary>
    public Action<MigrationCheckpoint> BeforeWrite { get; set; }

    /// <summary>
    /// Cancels the token on this call and then writes normally, so the stop surfaces from the source's next batch.
    /// </summary>
    public (int CallNumber, CancellationTokenSource Source)? CancelOnCall { get; set; }
    /// <summary>
    /// Cancels the token on this call and throws instead of writing, so the stop surfaces from the write itself.
    /// </summary>
    public (int CallNumber, CancellationTokenSource Source)? StopOnCall { get; set; }
    int callCount;

    public void SeedExistingKey(string sourceId) => preExistingKeys.Add(sourceId);

    public void RejectKey(string sourceId, MigrationSkipReason reason, bool benign = false) => rejectedKeys[sourceId] = (reason, benign);

    public IReadOnlyList<MigrationRow> WrittenRows(string categoryId) =>
        writtenRowsByCategory.TryGetValue(categoryId, out var rows) ? rows : [];

    /// <summary>
    /// Every row the engine sent to a write that ran, in the order it sent them, including the rows this fake then skipped or found already present. It is the only way to see the same row sent twice, because WrittenRows de-duplicates as the real targets do. Empty for a category never written to.
    /// </summary>
    public IReadOnlyList<MigrationRow> RowsHandedToWrite(string categoryId) =>
        rowsHandedToWriteByCategory.TryGetValue(categoryId, out var rows) ? rows : [];

    public Task Open(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public int BatchSizeFor(MigrationCategory category) =>
        category.Id == NoBatchSizeFor ? throw new InvalidOperationException($"No batch size is mapped for category {category.Id}") : DefaultBatchSize;

    public async Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointToExtend,
        CancellationToken cancellationToken = default)
    {
        callCount++;
        BeforeWrite?.Invoke(checkpointToExtend);

        if (StopOnCall is { } stop && stop.CallNumber == callCount)
        {
            await stop.Source.CancelAsync();
            throw new OperationCanceledException(stop.Source.Token);
        }

        if (FailOnCallNumber == callCount)
        {
            throw FailWith ?? new InvalidOperationException($"Simulated failure on write {callCount}");
        }

        if (CancelOnCall is { } cancel && cancel.CallNumber == callCount)
        {
            await cancel.Source.CancelAsync();
        }

        var keys = writtenKeysByCategory.TryGetValue(category.Id, out var existingKeys) ? existingKeys : writtenKeysByCategory[category.Id] = [];
        var rows = writtenRowsByCategory.TryGetValue(category.Id, out var existingRows) ? existingRows : writtenRowsByCategory[category.Id] = [];

        // Recorded here rather than at the top of the method: a write that threw above never committed, so
        // the restart that sends its batch again is doing the right thing.
        var handedOver = rowsHandedToWriteByCategory.TryGetValue(category.Id, out var existingHandedOver) ? existingHandedOver : rowsHandedToWriteByCategory[category.Id] = [];
        handedOver.AddRange(batch.Rows);

        var copied = 0;
        var alreadyPresent = 0;
        var benignSkipped = 0;
        var skippedIds = new List<string>();
        var skipReasons = new Dictionary<MigrationSkipReason, long>();

        foreach (var row in batch.Rows)
        {
            if (rejectedKeys.TryGetValue(row.SourceId, out var rejection))
            {
                skippedIds.Add(row.SourceId);
                skipReasons[rejection.Reason] = skipReasons.GetValueOrDefault(rejection.Reason) + 1;
                if (rejection.Benign)
                {
                    benignSkipped++;
                }
                continue;
            }

            if (preExistingKeys.Contains(row.SourceId) || !keys.Add(row.SourceId))
            {
                alreadyPresent++;
                continue;
            }

            rows.Add(row);
            copied++;
        }

        // The real targets extend and save the checkpoint in the transaction that writes the rows,
        // so this fake saves it here too.
        var saved = await checkpointStore.Upsert(
            checkpointToExtend.Extend(copied, skippedIds.Count, alreadyPresent, skipReasons),
            cancellationToken);

        return new MigrationWriteResult(saved, copied, skippedIds.Count, skippedIds, alreadyPresent, skipReasons, benignSkipped);
    }

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult((long)(writtenRowsByCategory.TryGetValue(category.Id, out var rows) ? rows.Count : 0));

    public IReadOnlyCollection<string> SupportedCategoryIds => [.. MigrationCategoryRegistry.All.Select(category => category.Id)];
}
