namespace Particular.ThroughputCollector.BrokerThroughput;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Transports;
using Shared;

internal class BrokerThroughputCollectorHostedService(
    ILogger<BrokerThroughputCollectorHostedService> logger,
    IBrokerThroughputQuery brokerThroughputQuery,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    TimeProvider timeProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"Starting {nameof(BrokerThroughputCollectorHostedService)}");

        await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken);

        using PeriodicTimer timer = new(TimeSpan.FromDays(1), timeProvider);

        try
        {
            do
            {
                try
                {
                    await GatherThroughput(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to gather throughput from broker");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation($"Stopping {nameof(BrokerThroughputCollectorHostedService)} timer");
        }
    }

    private async Task GatherThroughput(CancellationToken stoppingToken)
    {
        logger.LogInformation("Gathering throughput from broker");

        var waitingTasks = new List<Task>();

        await foreach (var queueName in brokerThroughputQuery.GetQueueNames(stoppingToken))
        {
            if (PlatformEndpointIdentifier.IsPlatformEndpoint(queueName.QueueName, throughputSettings))
            {
                continue;
            }

            var startDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-30);

            waitingTasks.Add(Exec(queueName, startDate));
        }

        await Task.WhenAll(waitingTasks);
        await dataStore.SaveBrokerData(throughputSettings.Broker, brokerThroughputQuery.ScopeType, brokerThroughputQuery.Data);
        return;

        async Task Exec(IBrokerQueue queueName, DateOnly startDate)
        {
            var endpointId = new EndpointIdentifier(queueName.QueueName, ThroughputSource.Broker);
            var endpoint = await dataStore.GetEndpoint(endpointId, stoppingToken);
            if (endpoint != null)
            {
                startDate = endpoint.DailyThroughput.Last().DateUTC;
            }

            await foreach (var queueThroughput in brokerThroughputQuery.GetThroughputPerDay(queueName, startDate, stoppingToken))
            {
                endpoint = new Endpoint(endpointId)
                {
                    Scope = queueName.Scope,
                    EndpointIndicators = queueName.EndpointIndicators.ToArray()
                };
                endpoint.DailyThroughput.Add(new EndpointDailyThroughput
                {
                    TotalThroughput = queueThroughput.TotalThroughput,
                    DateUTC = queueThroughput.DateUTC
                });

                if (brokerThroughputQuery.SupportsHistoricalMetrics)
                {
                    await dataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput, stoppingToken);
                }
                else
                {
                    await dataStore.AppendEndpointThroughput(endpoint);
                }
            }
        }
    }
}