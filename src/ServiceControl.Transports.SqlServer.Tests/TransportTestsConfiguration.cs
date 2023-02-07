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
        public IProvideQueueLength InitializeQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            var tempRandomDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRandomDirectory);

            var dbInstanceForSqlT = SqlLocalDb.CreateNewIn(tempRandomDirectory);

            var transportSettings = new TransportSettings
            {
                EndpointName = queueName,
                ConnectionString = dbInstanceForSqlT.ConnectionString,
                MaxConcurrency = 1
            };

            var factory = new RawEndpointFactory(transportSettings, customizations);

            var config = factory.CreateAuditIngestor(queueName, (context, dispatcher) => Task.CompletedTask);

            config.AutoCreateQueues(new string[] { $"{queueName}.Errors" });
            //No need to start the raw endpoint to create queues
            RawEndpoint.Create(config).ConfigureAwait(false).GetAwaiter().GetResult();

            var endpointConfiguration = new EndpointConfiguration(queueName);

            endpointConfiguration.EnableInstallers();
            customizations.CustomizeForMonitoringIngestion(endpointConfiguration, transportSettings);
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(dbInstanceForSqlT.ConnectionString, (qle, _) => onQueueLengthReported(qle.First()));

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        public Task Configure()
        {
            customizations = new SqlServerTransportCustomization();

            return Task.CompletedTask;
        }

        SqlServerTransportCustomization customizations;
    }
}