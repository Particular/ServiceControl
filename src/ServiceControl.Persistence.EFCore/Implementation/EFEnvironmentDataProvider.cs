namespace ServiceControl.Persistence.EFCore.Implementation;

using System;
using System.Globalization;
using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class EFEnvironmentDataProvider(EFPersisterSettings settings, IDatabaseHostingProbe hostingProbe, IStorageFootprintProbe footprintProbe, IServiceScopeFactory scopeFactory) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData()
    {
        // The hosting keys share one probe, so the database is asked once per report and they
        // stand or fall together, which is right because they have a single cause. The footprint
        // keys share theirs for the same reason.
        Task<DatabaseHosting>? probe = null;
        Task<DatabaseHosting> Hosting(CancellationToken cancellationToken) => probe ??= hostingProbe.Probe(cancellationToken);

        Task<StorageFootprint?>? footprint = null;
        Task<StorageFootprint?> Footprint(CancellationToken cancellationToken) => footprint ??= footprintProbe.Probe(cancellationToken);

        return
        [
            Value("Storage.Type", () => hostingProbe.StorageName),
            Deferred("Storage.Hosting", async cancellationToken => (await Hosting(cancellationToken)).Hosting),
            Deferred("Storage.ServerVersion", async cancellationToken => (await Hosting(cancellationToken)).ServerVersion),
            Deferred("Storage.HostingSource", async cancellationToken => (await Hosting(cancellationToken)).Source),
            Deferred("Storage.ServerEdition", async cancellationToken => (await Hosting(cancellationToken)).ServerEdition),
            Deferred("Storage.ServiceObjective", async cancellationToken => (await Hosting(cancellationToken)).ServiceObjective),
            Deferred("Storage.SizeGB", async cancellationToken => SizeGB((await Footprint(cancellationToken))?.SizeGB)),
            Deferred("Storage.MessageCount", async cancellationToken => Count((await Footprint(cancellationToken))?.MessageCount)),
            Deferred("Storage.UnresolvedFailedMessages", UnresolvedFailedMessages),
            Deferred("Health.Error.FailedImports", FailedImports),
            Deferred("Health.Error.RetentionBehindHours", RetentionBehindHours),
            Value("Storage.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
            Value("Storage.BodyStorage.Type", () => BodyStorageType(settings.BodyStorage)),
            Value("Limits.MaxBodySizeToStore", () => settings.BodyStorage.MaxBodySizeToStore.ToString(CultureInfo.InvariantCulture)),
            Value("Storage.Schema", () => settings.Schema is null ? "Default" : "Custom"),
            Value("Storage.CommandTimeoutSeconds", () => WhenConfigured(EFPersistenceConfigurationBase.CommandTimeoutKey, () => Number(settings.CommandTimeout))),
            Value("Storage.QueryTimeoutSeconds", () => WhenConfigured(QueryTimeLimit.SettingName, () => Seconds(settings.QueryTimeout))),
            Value("Storage.SubscriptionCacheSeconds", () => WhenConfigured(EFPersistenceConfigurationBase.SubscriptionCacheDurationKey, () => Seconds(settings.SubscriptionCacheDuration))),
            Value("Storage.BodyStorage.MinCompressionBytes", () => WhenConfigured(EFPersistenceConfigurationBase.MinBodySizeForCompressionKey, () => Number(settings.BodyStorage.MinCompressionSize))),
            Value("Storage.FreeSpaceThresholdPercent", FreeSpaceThreshold)
        ];
    }

    async ValueTask<string> UnresolvedFailedMessages(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var count = await dbContext.Set<FailedMessageEntity>().CountAsync(m => m.Status == FailedMessageStatus.Unresolved, cancellationToken);

        return Count(count);
    }

    async ValueTask<string> FailedImports(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var count = await dbContext.Set<FailedErrorImportEntity>().CountAsync(cancellationToken);

        return Count(count);
    }

    async ValueTask<string> RetentionBehindHours(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var oldest = await dbContext.Set<FailedMessageEntity>().MinAsync(m => (DateTime?)m.StatusChangedAt, cancellationToken);

        if (oldest is null)
        {
            return Count(0);
        }

        var behind = DateTime.UtcNow - settings.ErrorRetentionPeriod - oldest.Value;

        return Count(behind > TimeSpan.Zero ? (long)behind.TotalHours : 0);
    }

    static string SizeGB(double? sizeGB) =>
        sizeGB is null ? "Unknown" : sizeGB.Value.ToString("F1", CultureInfo.InvariantCulture);

    static string Count(long? count) =>
        count is null ? "Unknown" : count.Value.ToString(CultureInfo.InvariantCulture);

    string FreeSpaceThreshold() => settings.BodyStorage is FileSystemBodyStorageSettings fileSystem
        ? WhenConfigured(EFPersistenceConfigurationBase.FileSystemDataSpaceRemainingThresholdKey, () => Number(fileSystem.DataSpaceRemainingThreshold))
        : "NotApplicable";

    static string WhenConfigured(string key, Func<string> readValue) =>
        SettingsReader.TryRead<string>(SettingsNamespace, key, out _) ? readValue() : "Default";

    static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    static string Seconds(TimeSpan value) =>
        Math.Round(value.TotalSeconds, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);

    static string BodyStorageType(BodyStorageSettings bodyStorage) => bodyStorage switch
    {
        FileSystemBodyStorageSettings => nameof(Abstractions.BodyStorageType.FileSystem),
        AzureBlobBodyStorageSettings => nameof(Abstractions.BodyStorageType.AzureBlob),
        S3BodyStorageSettings => nameof(Abstractions.BodyStorageType.S3),
        _ => "Unknown"
    };

    static readonly SettingsRootNamespace SettingsNamespace = new("ServiceControl");
}
