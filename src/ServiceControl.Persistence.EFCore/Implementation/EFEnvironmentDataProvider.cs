namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Globalization;
using Abstractions;
using Infrastructure;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class EFEnvironmentDataProvider(EFPersisterSettings settings, IDatabaseHostingProbe hostingProbe) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData()
    {
        // The three hosting keys share one probe, so the database is asked once per report and they
        // stand or fall together, which is right because they have a single cause.
        Task<DatabaseHosting>? probe = null;
        Task<DatabaseHosting> Hosting(CancellationToken cancellationToken) => probe ??= hostingProbe.Probe(cancellationToken);

        return
        [
            Value("Storage.Type", () => hostingProbe.StorageName),
            Deferred("Storage.Hosting", async cancellationToken => (await Hosting(cancellationToken)).Hosting),
            Deferred("Storage.ServerVersion", async cancellationToken => (await Hosting(cancellationToken)).ServerVersion),
            Deferred("Storage.HostingSource", async cancellationToken => (await Hosting(cancellationToken)).Source),
            Value("Storage.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
            Value("Storage.BodyStorage.Type", () => BodyStorageType(settings.BodyStorage)),
            Value("Storage.BodyStorage.Auth", () => BodyStorageAuth(settings.BodyStorage)),
            Value("Limits.MaxBodySizeToStore", () => settings.BodyStorage.MaxBodySizeToStore.ToString(CultureInfo.InvariantCulture)),
            Value("Storage.Schema", () => settings.Schema is null ? "Default" : "Custom"),
            Value("Storage.CommandTimeoutSeconds", () => WhenConfigured(EFPersistenceConfigurationBase.CommandTimeoutKey, () => Number(settings.CommandTimeout))),
            Value("Storage.QueryTimeoutSeconds", () => WhenConfigured(QueryTimeLimit.SettingName, () => Seconds(settings.QueryTimeout))),
            Value("Storage.SubscriptionCacheSeconds", () => WhenConfigured(EFPersistenceConfigurationBase.SubscriptionCacheDurationKey, () => Seconds(settings.SubscriptionCacheDuration))),
            Value("Storage.BodyStorage.MinCompressionBytes", () => WhenConfigured(EFPersistenceConfigurationBase.MinBodySizeForCompressionKey, () => Number(settings.BodyStorage.MinCompressionSize))),
            Value("Storage.FreeSpaceThresholdPercent", FreeSpaceThreshold)
        ];
    }

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

    static string BodyStorageAuth(BodyStorageSettings bodyStorage) => bodyStorage switch
    {
        AzureBlobBodyStorageSettings azureBlob => azureBlob.Authentication switch
        {
            AzureBlobManagedIdentityAuthentication => "ManagedIdentity",
            AzureBlobSharedKeyAuthentication => "SharedKeyOrSas",
            _ => "Unknown"
        },
        S3BodyStorageSettings s3 => s3.Credentials is null ? "IamRole" : "StaticCredentials",
        _ => "NotApplicable"
    };

    static readonly SettingsRootNamespace SettingsNamespace = new("ServiceControl");
}
