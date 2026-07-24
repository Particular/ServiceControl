namespace ServiceControl.Persistence.EFCore.Implementation;

using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using ServiceControl.Persistence.EFCore.Abstractions;

static class AzureBlobClientFactory
{
    public static BlobContainerClient CreateContainerClient(EFPersisterSettings settings)
    {
        var serviceClient = settings.AzureBlobConnectionString is { Length: > 0 } connectionString
            ? new BlobServiceClient(connectionString)
            : new BlobServiceClient(new Uri(settings.AzureBlobServiceUri!), CreateCredential(settings));

        return serviceClient.GetBlobContainerClient(settings.AzureBlobContainerName);
    }

    static TokenCredential CreateCredential(EFPersisterSettings settings)
    {
        var options = new DefaultAzureCredentialOptions();

        // Steers the login endpoint for sovereign clouds; when unset the SDK honours the
        // AZURE_AUTHORITY_HOST environment variable.
        if (settings.AzureBlobAuthorityHost is { Length: > 0 } authorityHost)
        {
            options.AuthorityHost = new Uri(authorityHost);
        }

        if (settings.AzureBlobManagedIdentityClientId is { Length: > 0 } clientId)
        {
            options.ManagedIdentityClientId = clientId;
        }

        return new DefaultAzureCredential(options);
    }
}
