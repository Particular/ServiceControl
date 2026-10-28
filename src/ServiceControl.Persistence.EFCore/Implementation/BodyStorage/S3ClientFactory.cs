namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using ServiceControl.Persistence.EFCore.Abstractions;

static class S3ClientFactory
{
    public static IAmazonS3 Create(S3BodyStorageSettings settings)
    {
        var config = new AmazonS3Config();

        var serviceUrl = settings.ServiceUrl;
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true; // Required for S3-compatible endpoints (MinIO, LocalStack).
        }

        var region = settings.Region;
        if (!string.IsNullOrEmpty(region))
        {
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.AuthenticationRegion = region;
            }
            else
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            }
        }

        // With no static keys the SDK's default credential chain resolves the ambient IAM role.
        return settings.Credentials is { } credentials
            ? new AmazonS3Client(new BasicAWSCredentials(credentials.AccessKeyId, credentials.SecretAccessKey), config)
            : new AmazonS3Client(config);
    }
}
