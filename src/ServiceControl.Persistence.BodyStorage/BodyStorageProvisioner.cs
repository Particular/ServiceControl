namespace ServiceControl.Persistence.BodyStorage;

using Amazon.S3.Model;
using Amazon.S3.Util;

/// <summary>
/// Creates the directory, container or bucket that the selected body storage type writes to.
/// </summary>
public static class BodyStorageProvisioner
{
    public static Task Provision(BodyStorageSettings settings, CancellationToken cancellationToken = default) =>
        settings switch
        {
            FileSystemBodyStorageSettings fileSystem => ProvisionFileSystem(fileSystem, cancellationToken),
            AzureBlobBodyStorageSettings azureBlob => AzureBlobClientFactory.CreateContainerClient(azureBlob).CreateIfNotExistsAsync(cancellationToken: cancellationToken),
            S3BodyStorageSettings s3 => ProvisionS3(s3, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings, "Unknown body storage type.")
        };

    static Task ProvisionFileSystem(FileSystemBodyStorageSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(settings.StoragePath);

        return Task.CompletedTask;
    }

    static async Task ProvisionS3(S3BodyStorageSettings settings, CancellationToken cancellationToken)
    {
        using var client = S3ClientFactory.Create(settings);

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, settings.BucketName))
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = settings.BucketName }, cancellationToken);
        }
    }
}
