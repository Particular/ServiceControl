#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the endpoints ServiceControl has heard from, whole, out of the primary database.
/// </summary>
sealed class KnownEndpointsReader(RavenReadOnlySourceLifecycle lifecycle) : IMigrationCategoryReader
{
    public string CategoryId => MigrationCategoryIds.KnownEndpoints;

    public IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        RavenDocumentStream.ByPrefix<KnownEndpoint>(
            lifecycle,
            CategoryId,
            lifecycle.Settings.DatabaseName,
            RavenMonitoringDataStore.KnownEndpointsCollectionName + "/",
            resumeAfter,
            batchSize,
            RavenDocumentStream.WholeDocument,
            cancellationToken);
}
