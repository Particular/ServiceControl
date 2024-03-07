namespace Particular.ThroughputCollector.Broker
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Shared;

    internal class BrokerThroughputCollectorHostedService(
        ILogger<BrokerThroughputCollectorHostedService> logger,
        AzureQuery azureQuery,
        BrokerSettingValues brokerSettingValues)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting BrokerThroughputCollector Service");

            azureQuery.Initialise(brokerSettingValues.SettingValues);
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
            await foreach (QueueThroughput queueThroughput in azureQuery.Execute(DateTime.UtcNow, DateTime.UtcNow,
                               stoppingToken))
            {
                logger.LogInformation(queueThroughput.QueueName);
            }
        }

        private readonly ILogger logger = logger;
    }
}
