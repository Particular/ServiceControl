namespace Particular.License
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.License.Contracts;

    public class ThroughputCollectorHostedService : IHostedService
    {
        public ThroughputCollectorHostedService(ILogger logger, LicenseData licenseData)
        {
            this.logger = logger; //TODO not being passed in
            auditThroughputCollector = new AuditThroughputCollector(licenseData.ServiceControlAPI, logger);
            brokerThroughputCollector = new BrokerThroughputCollector(licenseData.Broker, logger);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting ThroughputCalculator Service");
            //return Task.Run(() => throughputGatherTimer = new Timer(objectstate => { GatherThrouput(); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)));
            throughputGatherTimer = new Timer(objectstate => { GatherThroughput(); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
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
