#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Operations;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Operations.BodyStorage.RavenAttachments;
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
        new EndpointSettingsReader(lifecycle),
        new FailedMessagesReader(lifecycle)
    }.ToFrozenDictionary(reader => reader.CategoryId, StringComparer.Ordinal);

    public Task Open(CancellationToken cancellationToken = default) => lifecycle.Open(cancellationToken);

    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() => [];

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

    // Counted through the reader the copy uses, not from RavenDB's collection statistics, because a collection does not map one to one onto a category.
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

    public async Task<MigrationBody?> ReadBody(MigrationCategory category, string sourceId, CancellationToken cancellationToken = default)
    {
        // Another category may keep its body inside the document, where a body attachment read would find nothing and skip every row as BodyMissing.
        if (category.Id != MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)
        {
            throw new NotSupportedException($"The migration source does not read bodies as attachments for the '{category.Id}' category.");
        }

        using var session = lifecycle.OpenSession(lifecycle.Settings.DatabaseName);

        var attachment = await session.Advanced.Attachments.GetAsync(sourceId, RavenAttachmentsBodyStorage.AttachmentName, cancellationToken);

        if (attachment == null)
        {
            return null;
        }

        await using var stream = attachment.Stream;

        var content = new MemoryStream((int)attachment.Details.Size);
        await stream.CopyToAsync(content, cancellationToken);

        return new MigrationBody(content.ToArray(), attachment.Details.ContentType);
    }

    public IReadOnlyCollection<string> SupportedCategoryIds => readers.Keys;

    public IReadOnlyDictionary<string, Type> DocumentTypes => readers.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.DocumentType, StringComparer.Ordinal);

    public ValueTask DisposeAsync() => lifecycle.DisposeAsync();

    // Counting only adds up row counts, so this size changes nothing but how often the stream stops to hand one back.
    const int CountBatchSize = 1024;
}
