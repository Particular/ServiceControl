namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NServiceBus;
    using NServiceBus.Raw;
    using Transports;
    using Transports.SQS;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new SQSTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for SQL transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            var transportConfig = c.UseTransport<SqsTransport>();
            transportConfig.ClientFactory(CreateSQSClient);
            transportConfig.ClientFactory(CreateSnsClient);

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                return;
            }

            var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };

            if (!builder.TryGetValue("QueueNamePrefix", out var queueNamePrefix))
            {
                return;
            }

            var queueNamePrefixAsString = (string)queueNamePrefix;
            if (!string.IsNullOrEmpty(queueNamePrefixAsString))
            {
                transportConfig.QueueNamePrefix(queueNamePrefixAsString);
            }
        }

        static IAmazonSQS CreateSQSClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSQSClient(credentials);
        }

        static IAmazonSimpleNotificationService CreateSnsClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSimpleNotificationServiceClient(credentials);
        }

        static string ConnectionStringKey = "ServiceControl.TransportTests.SQS.ConnectionString";
    }
}