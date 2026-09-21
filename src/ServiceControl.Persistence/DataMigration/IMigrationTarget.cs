namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implemented by a persister that can be the new database a migration copies into.
/// </summary>
public interface IMigrationTarget
{
    /// <summary>
    /// Makes the target ready to write. The host calls it before the copy and after the target's own checks have passed.
    /// </summary>
    Task Open(CancellationToken cancellationToken = default);

    /// <summary>
    /// The most rows a source may return in one batch for this category.
    /// The target picks it because its own database sets the limit, and a bigger batch fails the write.
    /// </summary>
    int BatchSizeFor(MigrationCategory category);

    /// <summary>
    /// Saves the batch's rows and the checkpoint in one transaction, so progress never gets ahead of the data.
    /// Add this batch's own outcome with <see cref="MigrationCheckpoint.Extend"/> and save that, so the stored counts are never a guess.
    /// </summary>
    /// <param name="checkpointToExtend">Prior totals, the cursor this batch reached, and any rows the engine itself skipped. Nothing the target does is counted yet.</param>
    Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointToExtend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows the target holds for one category, for progress and verify.
    /// Counts only that category, even where two categories share a table.
    /// </summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// The categories this target can write. A category outside this set is never handed to the engine.
    /// Answers before Open and does not change across it.
    /// </summary>
    IReadOnlyCollection<string> SupportedCategoryIds { get; }
}

/// <summary>
/// What the target did with one batch, and the checkpoint it committed alongside the rows. Every skipped row must have a reason in SkipReasons.
/// </summary>
/// <param name="Saved">The checkpoint as the target stored it, carrying the version that save landed on. The engine carries on from this one, never from the one it passed in.</param>
/// <param name="Copied">Rows this batch wrote. The same number must show up as the rise in the saved copied count, because the halt threshold reads one and the end-of-run reconciliation the other.</param>
/// <param name="Skipped">Rows this batch could not write, benign ones included.</param>
/// <param name="SkippedIds">The source ids of those rows, so the engine can name each one in the log.</param>
/// <param name="AlreadyPresent">Rows the target already held. They were neither copied nor skipped, and they still count as accounted for.</param>
/// <param name="SkipReasons">How many rows each reason skipped. The counts have to add up to Skipped, or the checkpoint refuses the batch.</param>
/// <param name="BenignSkipped">How many of Skipped the target would have deleted anyway, such as a row already past retention. Reported like any other skip, but never counted toward the halt threshold.</param>
public sealed record MigrationWriteResult(MigrationCheckpoint Saved, int Copied, int Skipped, IReadOnlyList<string> SkippedIds, int AlreadyPresent = 0, IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons = null, int BenignSkipped = 0);
