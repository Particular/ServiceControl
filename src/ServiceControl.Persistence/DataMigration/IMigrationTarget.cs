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
    /// Makes the target ready to write. Called once, before the copy starts.
    /// </summary>
    Task Open(CancellationToken cancellationToken = default);

    /// <summary>
    /// The most rows this target can write in one batch for the category. A bigger batch fails.
    /// </summary>
    Task<int> BatchSizeFor(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the batch's rows and the checkpoint in one transaction, so progress never gets ahead of the data.
    /// Build the checkpoint with <see cref="MigrationCheckpoint.Extend"/>.
    /// Bodies kept outside the database are written first, so a failure can leave an unused body but never a row without one.
    /// </summary>
    Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointToExtend,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows the target holds for one category, even where categories share a table.
    /// </summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a reset checkpoint so its category can be copied again, and returns it with its new version.
    /// Where the database makes the row keys, it also deletes the rows already copied, so none are copied twice.
    /// Throws <see cref="MigrationCheckpointConflictException" /> if another writer saved the checkpoint first.
    /// </summary>
    Task<MigrationCheckpoint> ResetForRetry(MigrationCheckpoint reset, CancellationToken cancellationToken = default);

    /// <summary>
    /// The categories this target can write. Known before Open and never changes.
    /// </summary>
    IReadOnlyCollection<string> SupportedCategoryIds { get; }

    /// <summary>
    /// The <see cref="MigrationRow.Document" /> type this target expects for each supported category.
    /// A row of any other type fails its batch. Known before Open and never changes.
    /// </summary>
    IReadOnlyDictionary<string, Type> DocumentTypes { get; }
}

/// <summary>
/// What the target did with one batch, and the checkpoint it saved with it. Every skipped row has a reason in SkipReasons.
/// </summary>
public sealed record MigrationWriteResult(MigrationCheckpoint Saved, int Copied, int Skipped, IReadOnlyList<string> SkippedIds, int AlreadyPresent = 0, IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons = null);
