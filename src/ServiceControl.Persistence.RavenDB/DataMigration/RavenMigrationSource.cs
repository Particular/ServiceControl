#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Operations;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.DataMigration.Readers;

/// <summary>
/// Reads a migration out of RavenDB. It holds one reader per category and knows nothing about any of them
/// beyond that, so a category this build cannot read is simply absent from <see cref="SupportedCategoryIds" />
/// and never reaches the engine.
/// </summary>
sealed class RavenMigrationSource(RavenReadOnlySourceLifecycle lifecycle) : IMigrationSource
{
    readonly FrozenDictionary<string, IMigrationCategoryReader> readers = new IMigrationCategoryReader[]
    {
        new KnownEndpointsReader(lifecycle),
        new EndpointSettingsReader(lifecycle)
    }.ToFrozenDictionary(reader => reader.CategoryId, StringComparer.Ordinal);

    public Task Open(CancellationToken cancellationToken = default) => lifecycle.Open(cancellationToken);

    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() => [new SourceDataVersionIsReadableCheck(lifecycle)];

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

    // Counted by streaming the same documents Read walks, not from RavenDB's collection statistics: a total that
    // counted anything Read leaves out would halt the category for a shortfall that never happened.
    public async Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default)
    {
        var total = 0L;

        await foreach (var batch in Read(category, resumeAfter: null, batchSize: CountBatchSize, cancellationToken))
        {
            total += batch.Rows.Count;
        }

        return total;
    }

    public IAsyncEnumerable<MigrationBatch> Read(
        MigrationCategory category,
        string? resumeAfter,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        readers.TryGetValue(category.Id, out var reader)
            ? reader.Read(resumeAfter, batchSize, cancellationToken)
            : throw new NotSupportedException($"The migration source cannot yet read the '{category.Id}' category.");

    public Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"The RavenDB migration source cannot read bodies for category {category.Id} yet");

    public IReadOnlyCollection<string> SupportedCategoryIds => readers.Keys;

    public ValueTask DisposeAsync() => lifecycle.DisposeAsync();

    // Counting only adds up row counts, so this size changes nothing but how often the stream stops to hand one back.
    const int CountBatchSize = 1024;
}
