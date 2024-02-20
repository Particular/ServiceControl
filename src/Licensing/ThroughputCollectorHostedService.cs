namespace Particular.License
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.License.Contracts;

    class ThroughputCollectorHostedService : IHostedService
    {
        public ThroughputCollectorHostedService(PlatformData platformData)
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            logger = factory.CreateLogger(typeof(ThroughputCollectorHostedService));

            auditThroughputCollector = new AuditThroughputCollector(platformData.ServiceControlAPI, logger);
            brokerThroughputCollector = new BrokerThroughputCollector(platformData.Broker, logger);

            //TODO initialise any persistence here, however it will need a separate method that is run during setup like in the Primary instance: PersistenceFactory.Create(settings);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting ThroughputCalculator Service");
            throughputGatherTimer = new Timer(objectstate => { GatherThroughput(); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); //TODO this will change to less often
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping ThroughputCalculator Service");
            return Task.CompletedTask;
        }

        void GatherThroughput()
        {
            //logger.LogInformation($"Gathering throughput from {serviceControlAPI}");
            auditThroughputCollector.GatherThroughput();
            brokerThroughputCollector.GatherThroughput();
        }

        AuditThroughputCollector auditThroughputCollector;
        BrokerThroughputCollector brokerThroughputCollector;
        readonly ILogger logger;
#pragma warning disable IDE0052 // Remove unread private members
        Timer? throughputGatherTimer;
#pragma warning restore IDE0052 // Remove unread private members
    }
}
