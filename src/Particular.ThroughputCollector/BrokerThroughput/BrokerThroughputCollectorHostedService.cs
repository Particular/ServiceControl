namespace Particular.ThroughputCollector.Broker
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ThroughputCollector.Contracts;

    class BrokerThroughputCollectorHostedService : BackgroundService
    {
        public BrokerThroughputCollectorHostedService(ILogger<BrokerThroughputCollectorHostedService> logger, ThroughputSettings throughputSettings)
        {
            this.logger = logger;
            this.throughputSettings = throughputSettings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting BrokerThroughputCollector Service");

            // When the timer should have no due-time, then do the work once now.
            await GatherThroughput(stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await GatherThroughput(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Stopping BrokerThroughputCollector Service");
            }
        }

        Task GatherThroughput(CancellationToken _)
        {
            logger.LogInformation($"Gathering throughput from broker");
            return Task.CompletedTask;
        }

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
    }
}
