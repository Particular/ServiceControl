namespace Particular.ThroughputCollector.Broker;

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

        var waitingTasks = new List<Task>();
        await foreach (var queueName in throughputQuery.GetQueueNames(stoppingToken))
        {
            if (IgnoreQueue(queueName.QueueName))
            {
                continue;
            }

            var startDate = DateTime.UtcNow.Date.AddDays(-30);

            waitingTasks.Add(Exec(queueName, startDate));
        }

        await Task.WhenAll(waitingTasks);
        return;

        async Task Exec(IQueueName queueName, DateTime startDate)
        {
            var endpoint = await dataStore.GetEndpointByName(queueName.QueueName, ThroughputSource.Broker);
            if (endpoint != null)
            {
                startDate = endpoint.DailyThroughput.Last().DateUTC;
            }

            await foreach (var queueThroughput in throughputQuery.GetThroughputPerDay(queueName,
                startDate,
                stoppingToken))
            {
                endpoint = new Endpoint
                {
                    Name = queueName.QueueName,
                    ThroughputSource = ThroughputSource.Broker
                };
                endpoint.DailyThroughput.Add(new EndpointThroughput
                {
                    TotalThroughput = queueThroughput.TotalThroughput,
                    DateUTC = queueThroughput.DateUTC
                }
                );


                await dataStore.AppendEndpointThroughput(endpoint);
            }
        }
    }

    private bool IgnoreQueue(string queueName)
    {
        if (queueName.Equals(throughputSettings.AuditQueue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queueName.Equals(throughputSettings.ErrorQueue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queueName.Equals(throughputSettings.ServiceControlQueue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queueName.EndsWith(".Timeouts", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queueName.EndsWith(".TimeoutsDispatcher", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private readonly ILogger logger = logger;
}