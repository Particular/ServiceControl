namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Globalization;
using Abstractions;
using Infrastructure;
using Particular.LicensingComponent.Contracts;
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
            Value("Persistence.Type", () => hostingProbe.StorageName),
            Deferred("Persistence.Hosting", async cancellationToken => (await Hosting(cancellationToken)).Hosting),
            Deferred("Persistence.ServerVersion", async cancellationToken => (await Hosting(cancellationToken)).ServerVersion),
            Deferred("Persistence.HostingSource", async cancellationToken => (await Hosting(cancellationToken)).Source),
            Value("Persistence.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
            Value("Persistence.BodyStorage.Type", () => BodyStorageType(settings.BodyStorage)),
            Value("Persistence.BodyStorage.Auth", () => BodyStorageAuth(settings.BodyStorage)),
            Value("Limits.MaxBodySizeToStore", () => settings.BodyStorage.MaxBodySizeToStore.ToString(CultureInfo.InvariantCulture))
        ];
    }

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
}
