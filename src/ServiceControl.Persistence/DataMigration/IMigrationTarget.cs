namespace ServiceControl.Persistence.DataMigration;

using System;
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
    Task<int> BatchSizeFor(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the batch's rows and the checkpoint in one transaction, so progress never gets ahead of the data.
    /// Add this batch's own outcome with <see cref="MigrationCheckpoint.Extend"/> and save that, so the stored counts are never a guess.
    /// For a category that carries bodies, every row must have its body.
    /// Bodies that go to external storage are written first, outside the transaction. So a failed write can leave a
    /// stored body that no row points to, but never a row that points to a body that was not stored.
    /// </summary>
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

    /// <summary>
    /// The type of <see cref="MigrationRow.Document" /> this target takes, for each category in
    /// <see cref="SupportedCategoryIds" />. A row holding any other type fails its batch with an
    /// <see cref="InvalidCastException" />. Answers before Open and does not change across it.
    /// </summary>
    IReadOnlyDictionary<string, Type> DocumentTypes { get; }
}

/// <summary>
/// What the target did with one batch, and the checkpoint it committed alongside the rows. Every skipped row must have a reason in SkipReasons.
/// </summary>
public sealed record MigrationWriteResult(MigrationCheckpoint Saved, int Copied, int Skipped, IReadOnlyList<string> SkippedIds, int AlreadyPresent = 0, IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons = null);
