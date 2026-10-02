namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public enum MigrationCategoryState
{
    NotStarted,
    InProgress,
    Complete,
    CompleteWithErrors,
    Halted,
    Abandoned,
    Blocked
}

/// <summary>A category's saved progress: where a restart carries on from, and what the status and verify commands report.</summary>
public sealed record MigrationCheckpoint(
    string CategoryId,
    MigrationCategoryState State,
    string? Cursor,
    long CopiedCount,
    long SkippedCount,
    long? SourceTotal,
    IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons,
    DateTime? StartedAt,
    DateTime? LastProgressAt,
    // The moment the category stopped running, whatever state it stopped in. Read it beside State: a halt settles too.
    DateTime? SettledAt,
    string? LastError,
    long AlreadyPresentCount = 0,
    // The optimistic concurrency token. A store sets it on save and refuses one carrying a value the stored row no longer holds.
    long Version = 0)
{
    /// <summary>Adds one batch's outcome to this checkpoint. A target calls it inside the transaction that writes the rows, so the saved counts are the real ones.</summary>
    public MigrationCheckpoint Extend(int copied, int skipped, int alreadyPresent, IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons)
    {
        var explained = skipReasons?.Values.Sum() ?? 0;
        if (explained != skipped)
        {
            throw new InvalidOperationException($"The target reported {skipped} skipped rows in category {CategoryId} but gave reasons for {explained}. Every skipped row needs a reason, or --migration-verify cannot account for it.");
        }

        return this with
        {
            CopiedCount = CopiedCount + copied,
            SkippedCount = SkippedCount + skipped,
            AlreadyPresentCount = AlreadyPresentCount + alreadyPresent,
            SkipReasons = AddSkipReasons(SkipReasons, skipReasons)
        };
    }

    internal static IReadOnlyDictionary<MigrationSkipReason, long>? AddSkipReasons(IReadOnlyDictionary<MigrationSkipReason, long>? totals, IReadOnlyDictionary<MigrationSkipReason, long>? additions)
    {
        if (additions is not { Count: > 0 })
        {
            return totals;
        }

        Dictionary<MigrationSkipReason, long> sum = totals is null ? [] : new(totals);
        foreach (var (reason, count) in additions)
        {
            sum[reason] = sum.GetValueOrDefault(reason) + count;
        }

        return sum;
    }
}

public interface IMigrationCheckpointStore
{
    Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default);
    Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default);

    /// <summary>Saves the checkpoint and returns it as stored, carrying the version the save landed on. Throws <see cref="MigrationCheckpointConflictException"/> when the stored row has moved on.</summary>
    Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
