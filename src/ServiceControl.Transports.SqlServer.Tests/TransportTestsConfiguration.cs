namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using Transports;
    using Transports.SqlServer;

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
            customizations = new SqlServerTransportCustomization();
            connectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for SQL transport tests to run");
            }

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<SqlServerTransport>()
                .ConnectionString(connectionString);
        }

        SqlServerTransportCustomization customizations;
        string connectionString;

        static string ConnectionStringKey = "ServiceControl.TransportTests.SQL.ConnectionString";
    }
}