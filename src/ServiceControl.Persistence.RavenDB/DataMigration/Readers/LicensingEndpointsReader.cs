#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

/// <summary>
/// Reads the licensing endpoints out of the throughput database, skipping every document outside the EndpointDocuments collection.
/// </summary>
sealed class LicensingEndpointsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<Endpoint>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.LicensingEndpoints;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        RavenDocumentStream.ByPrefix<EndpointDocument>(Lifecycle, CategoryId, Lifecycle.Settings.ThroughputDatabaseName, string.Empty, resumeAfter, batchSize, RavenDocumentStream.MaxDocumentsWalkedPerBatch, ThroughputDocuments.Endpoint, cancellationToken);
}
