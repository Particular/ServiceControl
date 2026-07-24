namespace ServiceControl.Persistence.EFCore.Implementation;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using ServiceControl.Persistence.EFCore.Abstractions;

static class S3ClientFactory
{
    public static IAmazonS3 Create(EFPersisterSettings settings)
    {
        var config = new AmazonS3Config();

        var serviceUrl = settings.S3ServiceUrl;
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true; // Required for S3-compatible endpoints (MinIO, LocalStack).
        }

        var region = settings.S3Region;
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
        return !string.IsNullOrEmpty(settings.S3AccessKeyId) && !string.IsNullOrEmpty(settings.S3SecretAccessKey)
            ? new AmazonS3Client(new BasicAWSCredentials(settings.S3AccessKeyId, settings.S3SecretAccessKey), config)
            : new AmazonS3Client(config);
    }
}
