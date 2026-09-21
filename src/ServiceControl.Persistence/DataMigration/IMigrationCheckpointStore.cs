namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Where one category has got to. <see cref="MigrationCategoryStateExtensions.IsFinished" /> says which of these
/// let the host open, and a restart picks up every category that is not one of those.
/// </summary>
public enum MigrationCategoryState
{
    /// <summary>No run has read a row of this category yet.</summary>
    NotStarted,

    /// <summary>A run is copying this category, or a run stopped without settling it.</summary>
    InProgress,

    /// <summary>The category reached the end of the source with nothing skipped.</summary>
    Complete,

    /// <summary>The category reached the end of the source, but some rows were skipped and stay only in the old database.</summary>
    CompleteWithErrors,

    /// <summary>Too many rows failed, so the category stopped and waits for a person. Fix the cause and restart to carry on from the cursor.</summary>
    Halted,

    /// <summary>A person accepted the loss and stopped copying this category. Nothing copies it again, even in a later migration.</summary>
    Abandoned,

    /// <summary>This category has to follow another one, and that one is not finished, so it did not run. A restart clears it once the other one settles.</summary>
    Blocked
}

/// <summary>
/// A category's saved progress: where a restart carries on from, and what the status and verify commands report.
/// Every count is the total across every run, not this run alone.
/// </summary>
/// <param name="Cursor">The point the last committed batch reached. A restart reads the source after it. Null means nothing has been read.</param>
/// <param name="SourceTotal">How many rows the source held when the category first started. It is captured once, so a source that has grown since does not move it.</param>
/// <param name="SkipReasons">How many rows each reason skipped. The counts here add up to <paramref name="SkippedCount" />.</param>
/// <param name="SettledAt">The moment the category stopped running, whatever state it stopped in. Read it beside <paramref name="State" />, because a halt settles too.</param>
/// <param name="LastError">Why the category halted, in the words the operator is shown. Null when it has not halted.</param>
/// <param name="AlreadyPresentCount">Rows the target already held, so they were neither copied nor skipped. They still count as accounted for when the run checks the category against <paramref name="SourceTotal" />.</param>
/// <param name="Version">The optimistic concurrency token, which is the guard against two writers. A store sets it on save and refuses a checkpoint carrying a value the stored row no longer holds.</param>
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
    DateTime? SettledAt,
    string? LastError,
    long AlreadyPresentCount = 0,
    long Version = 0)
{
    /// <summary>
    /// Adds one batch's outcome to this checkpoint. A target calls it inside the transaction that writes the rows,
    /// so the saved counts are the real ones.
    /// </summary>
    /// <param name="copied">Rows this batch wrote.</param>
    /// <param name="skipped">Rows this batch could not write. Every one of them needs a reason.</param>
    /// <param name="alreadyPresent">Rows this batch found the target already held.</param>
    /// <param name="skipReasons">How many rows each reason skipped in this batch.</param>
    /// <returns>A copy with this batch's counts and reasons added to the totals.</returns>
    /// <exception cref="InvalidOperationException">The reasons do not add up to <paramref name="skipped" />, which would leave the verify command unable to account for a row.</exception>
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

/// <summary>
/// Where the checkpoints are kept. The target database holds them, so progress and the rows it describes
/// commit together.
/// </summary>
public interface IMigrationCheckpointStore
{
    /// <summary>
    /// Every checkpoint the store holds. A category no run has started has no row, so it is absent rather than
    /// returned as not started.
    /// </summary>
    Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// One category's checkpoint, or null when no run has started it.
    /// </summary>
    Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the checkpoint and returns it as stored, carrying the version the save landed on.
    /// </summary>
    /// <exception cref="MigrationCheckpointConflictException">The stored row has moved on, which means another writer saved it, so this save is refused.</exception>
    Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
