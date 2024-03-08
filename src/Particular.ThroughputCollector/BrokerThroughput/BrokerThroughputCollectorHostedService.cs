namespace Particular.ThroughputCollector.Broker
{
    using Contracts;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Persistence;

    internal class BrokerThroughputCollectorHostedService(
        ILogger<BrokerThroughputCollectorHostedService> logger,
        IThroughputQuery throughputQuery,
        ThroughputSettings throughputSettings,
        IThroughputDataStore dataStore)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting BrokerThroughputCollector Service");

            throughputQuery.Initialise(throughputSettings.BrokerSettingValues);

            using PeriodicTimer timer = new(TimeSpan.FromDays(1));

            try
            {
                do
                {
                    await GatherThroughput(stoppingToken);
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Stopping BrokerThroughputCollector Service");
            }
        }

        private async Task GatherThroughput(CancellationToken stoppingToken)
        {
            logger.LogInformation("Gathering throughput from broker");

            await foreach (string queueName in throughputQuery.GetQueueNames(stoppingToken))
            {
                Endpoint? endpoint = await dataStore.GetEndpointByName(queueName, ThroughputSource.Broker);
                DateTime startDate = DateTime.UtcNow.Date.AddDays(-30);
                if (endpoint != null)
                {
                    startDate = endpoint.DailyThroughput.Last().DateUTC;
                }

                await foreach (EndpointThroughput queueThroughput in throughputQuery.GetThroughputPerDay(queueName,
                                   startDate,
                                   stoppingToken))
                {
                    await dataStore.RecordEndpointThroughput(new Endpoint
                    {
                        Name = queueName,
                        ThroughputSource = ThroughputSource.Broker,
                        DailyThroughput =
                        [
                            new EndpointThroughput
                            {
                                TotalThroughput = queueThroughput.TotalThroughput,
                                // force next line
                                DateUTC = queueThroughput.DateUTC
                            }
                        ]
                    });
                }
            }
        }

        private readonly ILogger logger = logger;
    }
}
