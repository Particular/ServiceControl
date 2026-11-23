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
    /// The category stopped early. It waits for the operator to retry or abandon it, and no start re-reads it.
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
/// Every count is the total across every run, and the skip reasons add up to SkippedCount. A store refuses a save
/// whose Version the stored row no longer holds, which is the guard against two writers.
/// </summary>
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
    /// The window, in whole seconds, that an optional category started with. It is always null for a required category.
    /// </summary>
    public long? StartedWindowSeconds { get; init; } = StartedWindowSeconds;

    /// <summary>
    /// The source's row count that --start-data-migration took before the copy, or null when nothing counted the category.
    /// The copy's progress lines and the Done column of --migration-status show the rows done as a share of it.
    /// </summary>
    public long? SourceTotal { get; init; } = SourceTotal;

    /// <summary>
    /// The moment the category stopped running, in any state. Read it beside State, because a halt settles too.
    /// </summary>
    public DateTime? SettledAt { get; init; } = SettledAt;

    /// <summary>
    /// Why the category stopped or did not run, in the words the operator is shown: a halt, a category it must follow that
    /// is not finished, an error that left an optional category copying, or a source that would not open. It stays until a start runs the category again.
    /// </summary>
    public string? LastError { get; init; } = LastError;

    /// <summary>
    /// Returns a copy with one batch's counts and skip reasons added to the totals. A target calls it inside the
    /// transaction that writes the rows, so the saved counts are the real ones. Throws InvalidOperationException when
    /// the skip reasons do not add up to the skipped rows, because verify could not account for the rest.
    /// </summary>
    public MigrationCheckpoint Extend(int copied, int skipped, int alreadyPresent, IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons)
    {
        var explained = skipReasons?.Values.Sum() ?? 0;
        if (explained != skipped)
        {
            throw new InvalidOperationException($"The target reported {skipped} skipped rows in category {CategoryId} but gave reasons for {explained}. Every skipped row needs a reason, or the verify step of --start-data-migration cannot account for it.");
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
