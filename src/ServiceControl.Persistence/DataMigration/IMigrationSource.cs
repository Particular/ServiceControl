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
    /// Connects to the source read-only. Every other member except <see cref="SupportedCategoryIds" /> and
    /// <see cref="Configuration" /> throws until this has run.
    /// </summary>
    Task Open(CancellationToken cancellationToken = default);

    /// <summary>
    /// The checks this source wants run before the copy starts, in the order they must run. Call it after Open.
    /// </summary>
    IReadOnlyList<IMigrationStartupCheck> ContributedChecks();

    /// <summary>
    /// The settings the source reads, each with where it came from. Answers before Open, so printing them opens nothing.
    /// </summary>
    IReadOnlyList<MigrationSettingValue> Configuration { get; }

    /// <summary>
    /// The version of the server the source reads. Call it after Open.
    /// </summary>
    Task<string> Version(CancellationToken cancellationToken = default);

    /// <summary>
    /// How many rows the source holds for one category, which the verify step of --start-data-migration reports.
    /// </summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a category in batches, after the checkpoint cursor, or from the start when it is null. Throws on a cursor it never issued.
    /// </summary>
    IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one row's message body when Read did not attach it. The engine calls it only for a category that
    /// carries bodies, and only for a row whose body Read left null. Returns null when the source has no body for the row.
    /// </summary>
    Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The categories this source can read. A category outside this set is never handed to the engine.
    /// Answers before Open and does not change across it.
    /// </summary>
    IReadOnlyCollection<string> SupportedCategoryIds { get; }

    /// <summary>
    /// The type of <see cref="MigrationRow.Document" /> in every row this source reads, for each category in
    /// <see cref="SupportedCategoryIds" />. The target's writer for the same category must take that type.
    /// Answers before Open and does not change across it.
    /// </summary>
    IReadOnlyDictionary<string, Type> DocumentTypes { get; }
}
