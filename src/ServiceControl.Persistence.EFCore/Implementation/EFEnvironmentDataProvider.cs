namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Globalization;
using Abstractions;
using Infrastructure;
using Particular.LicensingComponent.Contracts;

class EFEnvironmentDataProvider(EFPersisterSettings settings, IDatabaseHostingProbe hostingProbe) : IEnvironmentDataProvider
{
    public async Task<IEnumerable<(string key, string value)>> GetData(CancellationToken cancellationToken = default)
    {
        var hosting = await hostingProbe.Probe(cancellationToken);

        return
        [
            ("Persistence.Type", hostingProbe.StorageName),
            ("Persistence.Hosting", hosting.Hosting),
            ("Persistence.ServerVersion", hosting.ServerVersion),
            ("Persistence.FullTextSearch", settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
            ("Persistence.BodyStorage.Type", BodyStorageType(settings.BodyStorage)),
            ("Persistence.BodyStorage.Auth", BodyStorageAuth(settings.BodyStorage)),
            ("Limits.MaxBodySizeToStore", settings.BodyStorage.MaxBodySizeToStore.ToString(CultureInfo.InvariantCulture))
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
