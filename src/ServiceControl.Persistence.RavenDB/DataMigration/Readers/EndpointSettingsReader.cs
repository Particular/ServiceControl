#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the per-endpoint settings, whole, out of the primary database. The collection holds one document per
/// endpoint plus one with an empty name, which carries the default for every endpoint.
/// </summary>
sealed class EndpointSettingsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<EndpointSettings>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.EndpointSettings;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        WholeDocuments(EndpointSettingsStore.CollectionName + "/", resumeAfter, batchSize, cancellationToken);
}
