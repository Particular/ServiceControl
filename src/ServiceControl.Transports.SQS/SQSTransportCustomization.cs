namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Linq;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using BrokerThroughput;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public class SQSTransportCustomization(ILogger<SQSTransportCustomization> logger) : TransportCustomization<SqsTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings)
        {
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        //Do not ConfigurePubSub for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void AddTransportForPrimaryCore(IServiceCollection services,
            TransportSettings transportSettings)
        {
            services.AddSingleton<IBrokerThroughputQuery, AmazonSQSQuery>();
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        protected override SqsTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var builder = new SQSTransportConnectionString(transportSettings.ConnectionString);

            IAmazonSQS sqsClient;
            IAmazonSimpleNotificationService snsClient;

            bool alwaysLoadFromEnvironmentVariable = false;
            if (builder.AccessKey != null || builder.SecretKey != null)
            {
                PromoteEnvironmentVariableFromConnectionString(builder.AccessKey, "AWS_ACCESS_KEY_ID");
                PromoteEnvironmentVariableFromConnectionString(builder.SecretKey, "AWS_SECRET_ACCESS_KEY");

                PromoteEnvironmentVariableFromConnectionString(builder.Region, "AWS_REGION");
                _ = RegionEndpoint.EnumerableAllRegions
                        .SingleOrDefault(x => x.SystemName == builder.Region) ??
                    throw new ArgumentException($"Unknown region: \"{builder.Region}\"");

                // if the user provided the access key and secret access key they should always be loaded from environment credentials
                alwaysLoadFromEnvironmentVariable = true;
                sqsClient = new AmazonSQSClient(new EnvironmentVariablesAWSCredentials());
                snsClient = new AmazonSimpleNotificationServiceClient(new EnvironmentVariablesAWSCredentials());
            }
            else
            {
                //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
                logger.LogInformation(
                    "BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for SQS Client.");
                sqsClient = new AmazonSQSClient();
                snsClient = new AmazonSimpleNotificationServiceClient();
            }

            var transport = new SqsTransport(sqsClient, snsClient, disableUnrestrictedDelayedDelivery: true)
            {
                MaxAutoMessageVisibilityRenewalDuration = TimeSpan.Zero
            };

            if (!string.IsNullOrEmpty(builder.QueueNamePrefix))
            {
                transport.QueueNamePrefix = builder.QueueNamePrefix;
            }

            if (!string.IsNullOrEmpty(builder.TopicNamePrefix))
            {
                transport.TopicNamePrefix = builder.TopicNamePrefix;
            }

            if (!string.IsNullOrEmpty(builder.S3BucketForLargeMessages))
            {
                string keyPrefixAsString = string.Empty;
                if (builder.S3KeyPrefix != null)
                {
                    keyPrefixAsString = builder.S3KeyPrefix;
                }

                IAmazonS3 s3Client;
                if (alwaysLoadFromEnvironmentVariable)
                {
                    s3Client = new AmazonS3Client(new EnvironmentVariablesAWSCredentials());
                }
                else
                {
                    logger.LogInformation(
                        "BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for S3 Client.");
                    s3Client = new AmazonS3Client();
                }

                transport.S3 = new S3Settings(builder.S3BucketForLargeMessages, keyPrefixAsString, s3Client);
            }

            transport.DoNotWrapOutgoingMessages = builder.DoNotWrapOutgoingMessages;
            transport.ReserveBytesInMessageSizeCalculation = builder.ReservedBytesInMessageSize;

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        static void
            PromoteEnvironmentVariableFromConnectionString(string value, string environmentVariableName) =>
            Environment.SetEnvironmentVariable(environmentVariableName, value, EnvironmentVariableTarget.Process);

    }
}