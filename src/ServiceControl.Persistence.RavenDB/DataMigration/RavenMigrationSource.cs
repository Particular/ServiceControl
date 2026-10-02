#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
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

        var server = settings.UseEmbeddedServer
            ? new MigrationSourceFact("Server", settings.ServerUrl, $"{lifecycle.SettingsRoot}/{RavenBootstrapper.DatabaseMaintenancePortKey}")
            : new MigrationSourceFact("Server", settings.ConnectionString, $"{lifecycle.SettingsRoot}/{RavenBootstrapper.ConnectionStringKey}");

        return new MigrationSourceDescription(build.ProductVersion,
        [
            new MigrationSourceFact("Mode", settings.UseEmbeddedServer ? "embedded" : "external"),
            server,
            new MigrationSourceFact("Primary database", settings.DatabaseName, $"{lifecycle.SettingsRoot}/{RavenBootstrapper.DatabaseNameKey}"),
            new MigrationSourceFact("Throughput database", settings.ThroughputDatabaseName, $"{ThroughputSettings.SettingsNamespace}/{ThroughputSettings.DatabaseNameKey}")
        ]);
    }

    public async Task<IReadOnlyList<MigrationSourceInventoryEntry>> Inventory(CancellationToken cancellationToken = default)
    {
        var entries = new List<MigrationSourceInventoryEntry>();

        foreach (var databaseName in new[] { lifecycle.Settings.DatabaseName, lifecycle.Settings.ThroughputDatabaseName })
        {
            var statistics = await lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetCollectionStatisticsOperation(), cancellationToken);
            entries.AddRange(statistics.Collections.Select(collection => new MigrationSourceInventoryEntry(databaseName, collection.Key, collection.Value)));
        }

        return entries;
    }

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"The RavenDB migration source cannot count category {category.Id} yet");

    public IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"The RavenDB migration source cannot read category {category.Id} yet");

    public Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"The RavenDB migration source cannot read bodies for category {category.Id} yet");

    public ValueTask DisposeAsync() => lifecycle.DisposeAsync();
}
