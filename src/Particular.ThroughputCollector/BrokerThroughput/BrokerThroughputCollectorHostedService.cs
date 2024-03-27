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

            waitingTasks.Add(Exec(queueName));
        }

        await Task.WhenAll(waitingTasks);
        await dataStore.SaveBrokerData(throughputSettings.Broker, brokerThroughputQuery.ScopeType, brokerThroughputQuery.Data);
        return;

        async Task Exec(IBrokerQueue queueName)
        {
            var endpointId = new EndpointIdentifier(queueName.QueueName, ThroughputSource.Broker);
            var endpoint = await dataStore.GetEndpoint(endpointId, stoppingToken);

            if (endpoint == null)
            {
                endpoint = new Endpoint(endpointId)
                {
                    Scope = queueName.Scope,
                    EndpointIndicators = queueName.EndpointIndicators.ToArray()
                };

                await dataStore.SaveEndpoint(endpoint, stoppingToken);
            }

            var defaultStartDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-30);
            var startDate = endpoint.LastCollectedDate < defaultStartDate
                ? defaultStartDate
                : endpoint.LastCollectedDate;

            await foreach (var queueThroughput in throughputQuery.GetThroughputPerDay(queueName, startDate, stoppingToken))
            {
                await dataStore.RecordEndpointThroughput(queueName.QueueName, ThroughputSource.Broker, queueThroughput.DateUTC, queueThroughput.TotalThroughput, stoppingToken);
            }
        }
    }
}