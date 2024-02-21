namespace Particular.License.Throughput.Broker
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.License.Contracts;

    class BrokerThroughputCollectorHostedService : IHostedService
    {
        public BrokerThroughputCollectorHostedService(ILoggerFactory loggerFactory, PlatformData platformData)
        {
            logger = loggerFactory.CreateLogger<BrokerThroughputCollectorHostedService>();
            this.platformData = platformData;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting BrokerThroughputCollector Service");
            brokerThroughputGatherTimer = new Timer(_ => GatherThroughput(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); //TODO this will change to less often
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping BrokerThroughputCollector Service");
            brokerThroughputGatherTimer?.Dispose();
            return Task.CompletedTask;
        }

        void GatherThroughput()
        {
            logger.LogInformation($"Gathering throughput from broker");
        }

        readonly ILogger logger;
#pragma warning disable IDE0052 // Remove unread private members
        PlatformData platformData;
        Timer? brokerThroughputGatherTimer;
#pragma warning restore IDE0052 // Remove unread private members
    }
}
