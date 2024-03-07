namespace Particular.ThroughputCollector.Broker
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ThroughputCollector.Contracts;

    internal class BrokerThroughputCollectorHostedService(
        ILogger<BrokerThroughputCollectorHostedService> logger,
        AzureQuery azureQuery,
        ThroughputSettings throughputSettings)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting BrokerThroughputCollector Service");

            if (throughputSettings.Broker == Broker.AzureServiceBus)
            {
                azureQuery.Initialise(throughputSettings.BrokerSettingValues);
            }

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

        private async Task GatherThroughput(CancellationToken stoppingToken)
        {
            logger.LogInformation("Gathering throughput from broker");
            if (throughputSettings.Broker == Broker.AzureServiceBus)
            {
                await foreach (QueueThroughput queueThroughput in azureQuery.Execute(DateTime.UtcNow, DateTime.UtcNow,
                               stoppingToken))
                {
                    logger.LogInformation(queueThroughput.QueueName);
                }
            }
        }

        private readonly ILogger logger = logger;
    }
}
