#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the licensing endpoints out of the throughput database, skipping every document outside the EndpointDocuments collection.
/// </summary>
sealed class LicensingEndpointsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<Endpoint>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.LicensingEndpoints;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ThroughputDocuments.Endpoints(Lifecycle, CategoryId, resumeAfter, batchSize, cancellationToken);
}
