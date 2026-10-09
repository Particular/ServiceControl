namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Where one category has got to. <see cref="MigrationCategoryStateExtensions.IsFinished" /> says which of these
/// let the host open, and <see cref="MigrationCategoryStateExtensions.IsFailed" /> which wait for the operator. A
/// start picks up every category that is neither.
/// </summary>
public enum MigrationCategoryState
{
    /// <summary>No run has read a row of this category yet.</summary>
    NotStarted,

    /// <summary>
    /// A run is copying this category, or a run stopped without settling it. On an optional category, a LastError
    /// means an exception stopped it or a start could not open the source, and the next start resumes it.
    /// </summary>
    InProgress,

    /// <summary>
    /// The category reached the end of the source with no fault skips. Harmless ones stay counted.
    /// </summary>
    Complete,

    /// <summary>
    /// The category reached the end of the source with fault skips, so it is Failed until the operator retries or abandons it.
    /// </summary>
    CompleteWithErrors,

    /// <summary>
    /// The category stopped early. It waits for --migration-retry or --migration-abandon, and no start re-reads it.
    /// </summary>
    Halted,

    /// <summary>
    /// The operator gave up on a Failed or started required category, on any optional one, or on one whose rows
    /// depend on such a category. It is final: nothing copies it again, even in a later migration.
    /// </summary>
    Abandoned,

    /// <summary>
    /// This category follows a category that is not Done or Abandoned, so it did not run. A restart clears it once that one is.
    /// </summary>
    Blocked
}

/// <summary>
/// A category's saved progress: where a restart carries on from, and what the status and verify commands report.
/// Every count is the total across every run, not this run alone.
/// </summary>
/// <param name="Cursor">The point the last committed batch reached. A restart reads the source after it. Null means nothing has been read.</param>
/// <param name="SourceTotal">No longer written: the engine does not count the source, so this is null on every row it saves. The column stays only because dropping it is a schema change.</param>
/// <param name="SkipReasons">How many rows each reason skipped. The counts here add up to <paramref name="SkippedCount" />.</param>
/// <param name="SettledAt">The moment the category stopped running, whatever state it stopped in. Read it beside <paramref name="State" />, because a halt settles too.</param>
/// <param name="LastError">Why the category stopped or did not run, in the words the operator is shown: a halt, a category it must follow that is not finished, an exception that left an optional category copying, or a source a start could not open, on an optional category still copying or not started. It stays until a start runs the category again.</param>
/// <param name="AlreadyPresentCount">Rows the target already held, so they were neither copied nor skipped. They still count as accounted for.</param>
/// <param name="Version">The optimistic concurrency token, which is the guard against two writers. A store sets it on save and refuses a checkpoint carrying a value the stored row no longer holds.</param>
/// <param name="StartedWindowSeconds">The window, in whole seconds, an optional category started with. It is null until the copier writes it, and always null for a required category.</param>
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
    long Version = 0,
    long? StartedWindowSeconds = null)
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
    /// Every checkpoint the store holds. A run saves a not-started row for every category it will copy before it
    /// copies any of them, so a category is absent when no run has recorded it.
    /// </summary>
    Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// One category's checkpoint, or null when no run has recorded it.
    /// </summary>
    Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the checkpoint and returns it as stored, carrying the version the save landed on.
    /// </summary>
    /// <exception cref="MigrationCheckpointConflictException">The stored row has moved on, which means another writer saved it, so this save is refused.</exception>
    Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
