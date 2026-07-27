namespace ServiceControl.Persistence.EFCore.Implementation;

using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using ServiceControl.Persistence.EFCore.Abstractions;

static class AzureBlobClientFactory
{
    public static BlobContainerClient CreateContainerClient(AzureBlobBodyStorageSettings settings)
    {
        var serviceClient = settings.Authentication switch
        {
            AzureBlobSharedKeyAuthentication sharedKey => new BlobServiceClient(sharedKey.ConnectionString),
            AzureBlobManagedIdentityAuthentication managedIdentity => new BlobServiceClient(managedIdentity.ServiceUri, CreateCredential(managedIdentity)),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Authentication, "Unknown Azure Blob authentication.")
        };

        return serviceClient.GetBlobContainerClient(settings.ContainerName);
    }

    static TokenCredential CreateCredential(AzureBlobManagedIdentityAuthentication authentication)
    {
        var options = new DefaultAzureCredentialOptions();

        if (authentication.AuthorityHost is { } authorityHost)
        {
            options.AuthorityHost = authorityHost;
        }

        if (authentication.ClientId is { Length: > 0 } clientId)
        {
            options.ManagedIdentityClientId = clientId;
        }

        return new DefaultAzureCredential(options);
    }
}
