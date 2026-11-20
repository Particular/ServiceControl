#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the integration events the old instance had not yet dispatched, whole, out of the primary database.
/// </summary>
sealed class PendingIntegrationEventsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<ExternalIntegrationDispatchRequest>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.PendingIntegrationEvents;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        WholeDocuments(ExternalIntegrationRequestsDataStore.KeyPrefix + "/", resumeAfter, batchSize, cancellationToken);
}
