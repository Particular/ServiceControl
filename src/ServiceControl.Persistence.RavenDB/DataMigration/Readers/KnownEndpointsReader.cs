#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the endpoints ServiceControl has heard from, whole, out of the primary database.
/// </summary>
sealed class KnownEndpointsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<KnownEndpoint>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.KnownEndpoints;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        WholeDocuments(RavenMonitoringDataStore.KnownEndpointsCollectionName + "/", resumeAfter, batchSize, cancellationToken);
}
