#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Which documents one category is made of, and how they become rows. Read keeps the contract
/// <see cref="IMigrationSource.Read"/> sets out, for this one category.
/// </summary>
interface IMigrationCategoryReader
{
    /// <summary>The category this reader handles, named as in <see cref="MigrationCategoryIds" />.</summary>
    string CategoryId { get; }

    IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default);
}
