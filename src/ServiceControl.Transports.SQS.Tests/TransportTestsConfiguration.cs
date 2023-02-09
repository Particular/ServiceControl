namespace ServiceControl.Transport.Tests
{
    using System;
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
        public IProvideQueueLength InitializeQueueLengthProvider(Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(connectionString, store);

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        public Task Configure()
        {
            customizations = new SQSTransportCustomization();
            connectionString = Environment.GetEnvironmentVariable("ServiceControl.TransportTests.SQS.ConnectionString");

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            var transportConfig = c.UseTransport<SqsTransport>();
            transportConfig.ClientFactory(CreateSQSClient);
            transportConfig.ClientFactory(CreateSnsClient);
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

        string connectionString;
        SQSTransportCustomization customizations;
    }
}