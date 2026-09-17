namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Implemented by a persister that can be the new database a migration copies into.</summary>
public interface IMigrationTarget
{
    /// <summary>The most rows a source may return in one batch for this category. The target picks it because its own database sets the limit, and a batch over it fails the write.</summary>
    int BatchSizeFor(MigrationCategory category);

    /// <summary>Saves the batch's rows and the checkpoint in one transaction, so progress never gets ahead of the data. Extend checkpointToExtend with this batch's own outcome through <see cref="MigrationCheckpoint.Extend"/> and save the result, so what lands is the real split rather than a guess the next save has to correct.</summary>
    /// <param name="checkpointToExtend">Prior totals, the cursor this batch reached, and any rows the engine itself skipped. Not yet counting anything the target does.</param>
    Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointToExtend,
        CancellationToken cancellationToken = default);

    /// <summary>How many rows the target holds for one category, for progress and verify. Counts only that category, even where two categories share a table.</summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);
}

/// <summary>What the target did with one batch, and the checkpoint it committed alongside the rows. Every skipped row must have a reason in SkipReasons.</summary>
/// <param name="BenignSkipped">How many of Skipped the target would have deleted anyway, such as a row already past retention. Counted and reported like any skip, but never counted toward the halt threshold.</param>
public sealed record MigrationWriteResult(MigrationCheckpoint Saved, int Copied, int Skipped, IReadOnlyList<string> SkippedIds, int AlreadyPresent = 0, IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons = null, int BenignSkipped = 0);
