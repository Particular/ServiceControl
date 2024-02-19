namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Transports.RabbitMQ;
    using Transports;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new RabbitMQQuorumConventionalRoutingTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for RabbitMQ conventional routing with quorum queues transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        static string ConnectionStringKey = "ServiceControl_TransportTests_RabbitMQ_ConnectionString";
    }
}