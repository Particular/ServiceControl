namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Implemented by a persister that can be the new database a migration copies into.</summary>
public interface IMigrationTarget
{
    /// <summary>How many rows to read per batch for this category. The target picks it because its own database sets the limits.</summary>
    int BatchSizeFor(MigrationCategory category);

    /// <summary>Saves the batch's rows and checkpointAfterBatch in one go, so progress never gets ahead of the data. Save the checkpoint exactly as given, without adding counts to it.</summary>
    Task<MigrationWriteResult> Write(
        MigrationCategory category,
        MigrationBatch batch,
        MigrationCheckpoint checkpointAfterBatch,
        CancellationToken cancellationToken = default);

    /// <summary>How many rows the target holds for one category, for progress and verify. Counts only that category, even where two categories share a table.</summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);
}

/// <summary>What the target did with one batch. Every skipped row must have a reason in SkipReasons.</summary>
public sealed record MigrationWriteResult(int Copied, int Skipped, IReadOnlyList<string> SkippedIds, int AlreadyPresent = 0, IReadOnlyDictionary<string, long>? SkipReasons = null);
