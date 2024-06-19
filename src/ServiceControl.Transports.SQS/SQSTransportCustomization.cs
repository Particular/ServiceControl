namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;

    public class SQSTransportCustomization : TransportCustomization<SqsTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings)
        {
            var routing = new RoutingSettings(endpointConfiguration.GetSettings());
            routing.EnableMessageDrivenPubSubCompatibilityMode();
        }

        //Do not ConfigurePubSub for send-only endpoint
        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, SqsTransport transportDefinition, TransportSettings transportSettings) { }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        protected override SqsTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = transportSettings.ConnectionString };

            IAmazonSQS sqsClient;
            IAmazonSimpleNotificationService snsClient;

            bool alwaysLoadFromEnvironmentVariable = false;
            if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
            {
                PromoteEnvironmentVariableFromConnectionString(builder, "AccessKeyId", "AWS_ACCESS_KEY_ID");
                PromoteEnvironmentVariableFromConnectionString(builder, "SecretAccessKey", "AWS_SECRET_ACCESS_KEY");

                var region = PromoteEnvironmentVariableFromConnectionString(builder, "Region", "AWS_REGION");
                _ = RegionEndpoint.EnumerableAllRegions
                        .SingleOrDefault(x => x.SystemName == region) ??
                    throw new ArgumentException($"Unknown region: \"{region}\"");

                // if the user provided the access key and secret access key they should always be loaded from environment credentials
                alwaysLoadFromEnvironmentVariable = true;
                sqsClient = new AmazonSQSClient(new EnvironmentVariablesAWSCredentials());
                snsClient = new AmazonSimpleNotificationServiceClient(new EnvironmentVariablesAWSCredentials());
            }
            else
            {
                //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
                log.Info(
                    "BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for SQS Client.");
                sqsClient = new AmazonSQSClient();
                snsClient = new AmazonSimpleNotificationServiceClient();
            }

            var transport = new SqsTransport(sqsClient, snsClient);

            if (builder.TryGetValue("QueueNamePrefix", out object queueNamePrefix))
            {
                string queueNamePrefixAsString = (string)queueNamePrefix;
                if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                {
                    transport.QueueNamePrefix = queueNamePrefixAsString;
                }
            }

            if (builder.TryGetValue("TopicNamePrefix", out object topicNamePrefix))
            {
                string topicNamePrefixAsString = (string)topicNamePrefix;
                if (!string.IsNullOrEmpty(topicNamePrefixAsString))
                {
                    transport.TopicNamePrefix = topicNamePrefixAsString;
                }
            }

            if (builder.TryGetValue("S3BucketForLargeMessages", out object bucketForLargeMessages))
            {
                string bucketForLargeMessagesAsString = (string)bucketForLargeMessages;
                if (!string.IsNullOrEmpty(bucketForLargeMessagesAsString))
                {
                    string keyPrefixAsString = string.Empty;
                    if (builder.TryGetValue("S3KeyPrefix", out object keyPrefix))
                    {
                        keyPrefixAsString = (string)keyPrefix;
                    }

                    IAmazonS3 s3Client;
                    if (alwaysLoadFromEnvironmentVariable)
                    {
                        s3Client = new AmazonS3Client(new EnvironmentVariablesAWSCredentials());
                    }
                    else
                    {
                        log.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for S3 Client.");
                        s3Client = new AmazonS3Client();
                    }

                    transport.S3 = new S3Settings(bucketForLargeMessagesAsString, keyPrefixAsString, s3Client);
                }
            }

            if (builder.TryGetValue("DoNotWrapOutgoingMessages", out object doNotWrapOutgoingMessages) &&
                bool.TryParse(doNotWrapOutgoingMessages.ToString(), out bool doNotWrapOutgoingMessagesAsBool))
            {
                transport.DoNotWrapOutgoingMessages = doNotWrapOutgoingMessagesAsBool;
            }

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        static string PromoteEnvironmentVariableFromConnectionString(DbConnectionStringBuilder builder, string connectionStringKey, string environmentVariableName)
        {
            if (builder.TryGetValue(connectionStringKey, out var value))
            {
                var valueAsString = (string)value;
                Environment.SetEnvironmentVariable(environmentVariableName, valueAsString, EnvironmentVariableTarget.Process);
                return valueAsString;
            }

            throw new ArgumentException($"Missing value for '{connectionStringKey}'", connectionStringKey);
        }

        static readonly ILog log = LogManager.GetLogger<SQSTransportCustomization>();
    }
}