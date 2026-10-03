namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implemented by a persister that can be read as the old database a migration copies from. Nothing here
/// writes to the source, because the old database has to stay usable if the migration is thrown away.
/// </summary>
public interface IMigrationSource : IAsyncDisposable
{
    /// <summary>
    /// Connects to the source read-only. Every other member except <see cref="SupportedCategoryIds" /> throws
    /// until this has run.
    /// </summary>
    Task Open(CancellationToken cancellationToken = default);

    /// <summary>
    /// The checks this source wants run before the copy starts, in the order they must run. Call it after Open.
    /// </summary>
    IReadOnlyList<IMigrationStartupCheck> ContributedChecks();

    /// <summary>
    /// What the source report and the dry run print about the source.
    /// </summary>
    Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default);

    /// <summary>
    /// A count of everything the source holds, including data no category copies.
    /// </summary>
    Task<IReadOnlyList<MigrationSourceInventoryEntry>> Inventory(CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows the source holds for one category, for progress and verify.
    /// </summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a category in batches, after the checkpoint cursor, or from the start when it is null. Throws on a cursor it never issued.
    /// </summary>
    /// <param name="batchSize">A ceiling, not a target: returning fewer costs nothing, returning more fails the target's write.</param>
    IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one row's message body when Read did not attach it. Returns null when the row has no body.
    /// </summary>
    Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The categories this source can read. A category outside this set is never handed to the engine.
    /// Answers before Open and does not change across it.
    /// </summary>
    IReadOnlyCollection<string> SupportedCategoryIds { get; }
}
