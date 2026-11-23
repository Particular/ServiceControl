#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Configuration;
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
        new FailedMessagesReader(lifecycle),
        new LicensingEndpointsReader(lifecycle),
        new LicensingThroughputReader(lifecycle),
        new LicensingReportMasksReader(lifecycle),
        new LicensedEndpointDetailsReader(lifecycle),
        new MessageRedirectsReader(lifecycle),
        new SubscriptionsReader(lifecycle),
        new NotificationSettingsReader(lifecycle),
        new TrialEndDateReader(lifecycle),
        new RetryOperationsReader(lifecycle),
        new PendingIntegrationEventsReader(lifecycle),
        new CustomChecksReader(lifecycle),
        new FailedErrorImportsReader(lifecycle),
        new GroupCommentsReader(lifecycle)
    }.ToFrozenDictionary(reader => reader.CategoryId, StringComparer.Ordinal);

    public Task Open(CancellationToken cancellationToken = default) => lifecycle.Open(cancellationToken);

    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() => [];

    // Read once at creation, so each origin matches the settings the lifecycle was built from.
    public IReadOnlyList<MigrationSettingValue> Configuration { get; } = ReadConfiguration(lifecycle.Settings, lifecycle.SettingsRoot);

    public async Task<string> Version(CancellationToken cancellationToken = default) =>
        (await lifecycle.DocumentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken)).ProductVersion;

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) =>
        readers.TryGetValue(category.Id, out var reader)
            ? reader.Count(cancellationToken)
            : throw new NotSupportedException($"The migration source cannot yet read the '{category.Id}' category.");

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

    static IReadOnlyList<MigrationSettingValue> ReadConfiguration(RavenPersisterSettings settings, SettingsRootNamespace settingsRoot)
    {
        var connectionStringOrigin = MigrationSettingValue.OriginOf(settingsRoot, RavenBootstrapper.ConnectionStringKey);
        List<MigrationSettingValue> values = [new("Mode", settings.UseEmbeddedServer ? "embedded" : "external", connectionStringOrigin)];

        if (settings.UseEmbeddedServer)
        {
            values.Add(new("Server", settings.ServerUrl, MigrationSettingValue.OriginOf(settingsRoot, RavenBootstrapper.DatabaseMaintenancePortKey)));
            values.Add(new("Data directory", settings.DatabasePath!, MigrationSettingValue.OriginOf(settingsRoot, RavenBootstrapper.DatabasePathKey)));
        }
        else
        {
            values.Add(new("Server", settings.ConnectionString, connectionStringOrigin));
        }

        values.Add(new("Primary database", settings.DatabaseName, MigrationSettingValue.OriginOf(settingsRoot, RavenBootstrapper.DatabaseNameKey)));
        values.Add(new("Throughput database", settings.ThroughputDatabaseName, MigrationSettingValue.OriginOf(ThroughputSettings.SettingsNamespace, ThroughputSettings.DatabaseNameKey)));

        return values;
    }
}
