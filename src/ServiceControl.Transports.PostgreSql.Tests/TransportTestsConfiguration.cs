namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Transports.PostgreSql;
    using Transports;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure(CancellationToken cancellationToken = default)
        {
            TransportCustomization = new PostgreSqlTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for PostgreSQL transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;

        const string ConnectionStringKey = "ServiceControl_TransportTests_PostgreSQL_ConnectionString";
    }
}