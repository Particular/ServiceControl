namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Raw;

    public class SQSTransportCustomization : TransportCustomization
    {
        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<SqsTransport>();
            ConfigureTransport(transport, transportSettings);
            //Do not ConfigurePubSub for send-only endpoint
        }

        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<SqsTransport>();
            ConfigureTransport(transport, transportSettings);
            ConfigurePubSub(transport, transportSettings);
        }

        public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            var transport = endpointConfiguration.UseTransport<SqsTransport>();
            ConfigureTransport(transport, transportSettings);
        }

        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            CustomizeRawEndpoint(endpointConfiguration, transportSettings);
        }

        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new QueueLengthProvider();
        }

        static void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<SqsTransport>();
            transport.ApplyHacksForNsbRaw();

            ConfigureTransport(transport, transportSettings);
            ConfigurePubSub(transport, transportSettings);
        }

        static void ConfigurePubSub(TransportExtensions<SqsTransport> transport, TransportSettings transportSettings)
        {
            // precaution in case we would ever use the subscription manager
            transportSettings.Set("NServiceBus.AmazonSQS.DisableSubscribeBatchingOnStart", true);
            // will be removed in next major
#pragma warning disable 618
            transport.EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore 618
        }

        static void ConfigureTransport(TransportExtensions<SqsTransport> transport, TransportSettings transportSettings)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = transportSettings.ConnectionString };

            var alwaysLoadFromEnvironmentVariable = false;
            if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
            {
                PromoteEnvironmentVariableFromConnectionString(builder, "AccessKeyId", "AWS_ACCESS_KEY_ID");
                PromoteEnvironmentVariableFromConnectionString(builder, "SecretAccessKey", "AWS_SECRET_ACCESS_KEY");

                // if the user provided the access key and secret access key they should always be loaded from environment credentials
                alwaysLoadFromEnvironmentVariable = true;
                transport.ClientFactory(() => new AmazonSQSClient(new EnvironmentVariablesAWSCredentials()));
                transport.ClientFactory(() => new AmazonSimpleNotificationServiceClient(new EnvironmentVariablesAWSCredentials()));
            }
            else
            {
                //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
                log.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for SQS Client.");
            }

            var region = PromoteEnvironmentVariableFromConnectionString(builder, "Region", "AWS_REGION");

            var awsRegion = RegionEndpoint.EnumerableAllRegions
                .SingleOrDefault(x => x.SystemName == region);

            if (awsRegion == null)
            {
                throw new ArgumentException($"Unknown region: \"{region}\"");
            }

            if (builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
            {
                var queueNamePrefixAsString = (string)queueNamePrefix;
                if (!string.IsNullOrEmpty(queueNamePrefixAsString))
                {
                    transport.QueueNamePrefix(queueNamePrefixAsString);
                }
            }

            if (builder.TryGetValue("TopicNamePrefix", out var topicNamePrefix))
            {
                var topicNamePrefixAsString = (string)topicNamePrefix;
                if (!string.IsNullOrEmpty(topicNamePrefixAsString))
                {
                    transport.TopicNamePrefix(topicNamePrefixAsString);
                }
            }

            if (builder.TryGetValue("S3BucketForLargeMessages", out var bucketForLargeMessages))
            {
                var bucketForLargeMessagesAsString = (string)bucketForLargeMessages;
                if (!string.IsNullOrEmpty(bucketForLargeMessagesAsString))
                {
                    var keyPrefixAsString = string.Empty;
                    if (builder.TryGetValue("S3KeyPrefix", out var keyPrefix))
                    {
                        keyPrefixAsString = (string)keyPrefix;
                    }

                    var s3Settings = transport.S3(bucketForLargeMessagesAsString, keyPrefixAsString);
                    if (alwaysLoadFromEnvironmentVariable)
                    {
                        s3Settings.ClientFactory(() => new AmazonS3Client(new EnvironmentVariablesAWSCredentials()));
                    }
                    else
                    {
                        log.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials for S3 Client.");
                    }
                }
            }

            //HINT: This is needed to make Core doesn't load a connection string value from the app.config.
            //      This prevents SQS from throwing on startup.
            var connectionString = transport.GetSettings().Get("NServiceBus.TransportConnectionString");

            connectionString.GetType()
                .GetField("GetValue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(connectionString, (Func<string>)(() => null));

            transport.Transactions(TransportTransactionMode.ReceiveOnly);
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

        static ILog log = LogManager.GetLogger<SQSTransportCustomization>();
    }
}