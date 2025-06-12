namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure;
    using ServiceControl.Transports.PostgreSql;
    using Transports;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new PostgreSqlTransportCustomization(LoggerUtil.CreateStaticLogger<PostgreSqlTransportCustomization>());
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for PostgreSQL transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        const string ConnectionStringKey = "ServiceControl_TransportTests_PostgreSQL_ConnectionString";
    }
}