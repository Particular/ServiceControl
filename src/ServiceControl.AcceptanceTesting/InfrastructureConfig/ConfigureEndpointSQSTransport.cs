namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControlInstaller.Engine.Instances;
    using Transports.SQS;

    public class ConfigureEndpointSQSTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UseSerialization<NewtonsoftJsonSerializer>();

            var transportConfig = configuration.UseTransport<SqsTransport>();
            transportConfig.ClientFactory(CreateSQSClient);
            transportConfig.ClientFactory(CreateSnsClient);

            S3BucketName = Environment.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrEmpty(S3BucketName))
            {
                var s3Configuration = transportConfig.S3(S3BucketName, S3Prefix);
                s3Configuration.ClientFactory(CreateS3Client);
            }

            if (string.IsNullOrWhiteSpace(ConnectionString) == false)
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };

                if (builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
                {
                    var queueNamePrefixAsString = (string)queueNamePrefix;
                    if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                    {
                        transportConfig.QueueNamePrefix(queueNamePrefixAsString);
                    }
                }
            }

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AmazonSQS;

        public string TypeName => $"{typeof(SQSTransportCustomization).AssemblyQualifiedName}";

        public string ConnectionString { get; set; }

        public string ScrubPlatformConnection(string input)
        {
            var result = input;

            var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };

            if (builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
            {
                var queueNamePrefixAsString = (string)queueNamePrefix;
                if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                {
                    result = result.Replace(
                        queueNamePrefixAsString,
                        "queue-prefix-"
                    );
                }
            }

            return result;
        }

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

        static IAmazonSimpleNotificationService CreateSnsClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSimpleNotificationServiceClient(credentials);
        }

        const string S3Prefix = "test";

        const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";
        static string S3BucketName;
    }
}