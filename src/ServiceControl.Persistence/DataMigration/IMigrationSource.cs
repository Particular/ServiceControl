namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IMigrationSource : IAsyncDisposable
{
    /// <summary>Connects to the source read-only. Every other member throws until this has run.</summary>
    Task Open(CancellationToken cancellationToken = default);

    /// <summary>What the source report and dry run print about the source.</summary>
    Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default);

    /// <summary>A count of everything the source holds, including data no category copies.</summary>
    Task<IReadOnlyList<MigrationSourceInventoryEntry>> Inventory(CancellationToken cancellationToken = default);

    /// <summary>How many rows the source holds for one category, for progress and verify.</summary>
    Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default);

    /// <summary>Reads a category in batches, after the checkpoint cursor if provided, or from the start when it is null. Throws on a cursor it never issued.</summary>
    /// <param name="batchSize">A ceiling, not a target: returning fewer costs nothing, returning more fails the target's write.</param>
    IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one row's message body when Read did not attach it. Returns null when the row has no body.</summary>
    Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default);
}
