namespace ServiceControl.Persistence.EFCore.Implementation;

using Amazon.S3.Model;
using Amazon.S3.Util;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class S3BodyStorageInstaller(EFPersisterSettings settings) : IBodyStorageInstaller
{
    public async Task Provision(CancellationToken cancellationToken = default)
    {
        using var client = S3ClientFactory.Create(settings);

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, settings.S3BucketName))
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = settings.S3BucketName }, cancellationToken);
        }
    }
}
