namespace ServiceControl.Transport.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports.SqlServer.Tests;
    using Transports;
    using Transports.SqlServer;

    partial class TransportTestsConfiguration
    {
        public IProvideQueueLength InitializeQueueLengthProvider(Action<QueueLengthEntry> onQueueLengthReported)
        {
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(dbInstanceForSqlT.ConnectionString, (qle, _) => onQueueLengthReported(qle.First()));

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        public Task Configure()
        {
            var tempRandomDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRandomDirectory);

            dbInstanceForSqlT = SqlLocalDb.CreateNewIn(tempRandomDirectory);

            customizations = new SqlServerTransportCustomization();

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<SqlServerTransport>()
                .ConnectionString(dbInstanceForSqlT.ConnectionString);
        }

        SqlLocalDb dbInstanceForSqlT;
        SqlServerTransportCustomization customizations;
    }
}