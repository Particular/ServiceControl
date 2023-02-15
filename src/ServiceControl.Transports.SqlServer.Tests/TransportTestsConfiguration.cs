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
        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new SqlServerTransportCustomization();
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
            c.UseTransport<SqlServerTransport>()
                .ConnectionString(ConnectionString);
        }

        static string ConnectionStringKey = "ServiceControl.TransportTests.SQL.ConnectionString";
    }
}