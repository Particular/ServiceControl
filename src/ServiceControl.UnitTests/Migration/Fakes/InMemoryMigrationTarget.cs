namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

public sealed class InMemoryMigrationTarget(IMigrationCheckpointStore checkpointStore) : IMigrationTarget
{
    readonly Dictionary<string, HashSet<string>> writtenKeysByCategory = [];
    readonly Dictionary<string, List<MigrationRow>> writtenRowsByCategory = [];
    readonly HashSet<string> preExistingKeys = [];
    readonly Dictionary<string, string> rejectedKeys = [];

    public int DefaultBatchSize { get; set; } = 3;
    public int? FailOnCallNumber { get; set; }
    public (int CallNumber, CancellationTokenSource Source)? StopOnCall { get; set; }
    int callCount;

    public void SeedExistingKey(string sourceId) => preExistingKeys.Add(sourceId);

    public void RejectKey(string sourceId, string reason) => rejectedKeys[sourceId] = reason;

    public IReadOnlyList<MigrationRow> WrittenRows(string categoryId) =>
        writtenRowsByCategory.TryGetValue(categoryId, out var rows) ? rows : [];

    public int BatchSizeFor(MigrationCategory category) => DefaultBatchSize;

    public async Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointAfterBatch,
        CancellationToken cancellationToken = default)
    {
        callCount++;

        if (StopOnCall is { } stop && stop.CallNumber == callCount)
        {
            await stop.Source.CancelAsync();
            throw new OperationCanceledException(stop.Source.Token);
        }

        if (FailOnCallNumber == callCount)
        {
            throw new InvalidOperationException($"Simulated failure on write {callCount}");
        }

        var keys = writtenKeysByCategory.TryGetValue(category.Id, out var existingKeys) ? existingKeys : writtenKeysByCategory[category.Id] = [];
        var rows = writtenRowsByCategory.TryGetValue(category.Id, out var existingRows) ? existingRows : writtenRowsByCategory[category.Id] = [];

        var copied = 0;
        var alreadyPresent = 0;
        var skippedIds = new List<string>();
        var skipReasons = new Dictionary<string, long>();

        foreach (var row in batch.Rows)
        {
            if (rejectedKeys.TryGetValue(row.SourceId, out var reason))
            {
                skippedIds.Add(row.SourceId);
                skipReasons[reason] = skipReasons.GetValueOrDefault(reason) + 1;
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

        // Verbatim and in the same operation as the rows, as the real targets persist it. Adding the
        // batch's own counts here would double every number the engine already included.
        await checkpointStore.Upsert(checkpointAfterBatch, cancellationToken);

        return new MigrationWriteResult(copied, skippedIds.Count, skippedIds, alreadyPresent, skipReasons);
    }

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult((long)(writtenRowsByCategory.TryGetValue(category.Id, out var rows) ? rows.Count : 0));
}
