using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using NServiceBus;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointSQSTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        var transportConfig = configuration.UseTransport<SqsTransport>();
        transportConfig.ClientFactory(CreateSQSClient);

        S3BucketName = Environment.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

        if (!string.IsNullOrEmpty(S3BucketName))
        {
            var s3Configuration = transportConfig.S3(S3BucketName, S3Prefix);
            s3Configuration.ClientFactory(CreateS3Client);
        }

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }


    public string MonitoringSeamTypeName => $"{typeof(ServiceControl.Transports.AzureStorageQueues.ServiceControlAzureStorageQueueTransport).AssemblyQualifiedName}";

    public string ConnectionString { get; set; }

    static IAmazonSQS CreateSQSClient()
    {
        var credentials = new EnvironmentVariablesAWSCredentials();
        return new AmazonSQSClient(credentials);
    }

    static IAmazonS3 CreateS3Client()
    {
        var credentials = new EnvironmentVariablesAWSCredentials();
        return new AmazonS3Client(credentials);
    }

    const string S3Prefix = "test";

    const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";
    static string S3BucketName;
}