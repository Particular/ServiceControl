#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Operations;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Persistence.DataMigration;

sealed class RavenMigrationSource(RavenReadOnlySourceLifecycle lifecycle) : IMigrationSource
{
    public Task Open(CancellationToken cancellationToken = default) => lifecycle.Open(cancellationToken);

    public async Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default)
    {
        var settings = lifecycle.Settings;
        var build = await lifecycle.DocumentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

        return new MigrationSourceDescription(
            settings.UseEmbeddedServer,
            settings.UseEmbeddedServer ? settings.ServerUrl : settings.ConnectionString,
            settings.DatabaseName,
            settings.ThroughputDatabaseName,
            build.ProductVersion);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountCollections(MigrationSourceDatabase database, CancellationToken cancellationToken = default)
    {
        var databaseName = database == MigrationSourceDatabase.Primary
            ? lifecycle.Settings.DatabaseName
            : lifecycle.Settings.ThroughputDatabaseName;

        var statistics = await lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetCollectionStatisticsOperation(), cancellationToken);

        return statistics.Collections;
    }

    public ValueTask DisposeAsync() => lifecycle.DisposeAsync();
}
