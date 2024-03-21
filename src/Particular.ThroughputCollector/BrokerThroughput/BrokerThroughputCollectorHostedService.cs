namespace Particular.ThroughputCollector.Broker;

using Contracts;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;

internal class BrokerThroughputCollectorHostedService(
    ILogger<BrokerThroughputCollectorHostedService> logger,
    IThroughputQuery throughputQuery,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    TimeProvider timeProvider)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting BrokerThroughputCollector Service");

        throughputQuery.Initialise(throughputSettings.BrokerSettingValues);

        backgroundTimer = timeProvider.CreateTimer(async _ =>
        {
            try
            {
                await GatherThroughput(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to gather throughput from broker");
            }
        }, null, TimeSpan.FromSeconds(20), TimeSpan.FromDays(1));

        stoppingToken.Register(_ => backgroundTimer?.Dispose(), null);

        return Task.CompletedTask;
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

            var startDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-30);

            waitingTasks.Add(Exec(queueName, startDate));
        }

        await Task.WhenAll(waitingTasks);
        await dataStore.SaveBrokerData(throughputSettings.Broker, throughputQuery.ScopeType, throughputQuery.Data);
        return;

        async Task Exec(IQueueName queueName, DateOnly startDate)
        {
            var endpoint = await dataStore.GetEndpointByName(queueName.QueueName, ThroughputSource.Broker);
            if (endpoint != null)
            {
                startDate = endpoint.DailyThroughput.Last().DateUTC;
            }

            await foreach (var queueThroughput in throughputQuery.GetThroughputPerDay(queueName, startDate, stoppingToken))
            {
                endpoint = new Endpoint(queueName.QueueName, ThroughputSource.Broker)
                {
                    Scope = queueThroughput.Scope,
                    EndpointIndicators = queueThroughput.EndpointIndicators
                };
                endpoint.DailyThroughput.Add(new EndpointThroughput
                {
                    TotalThroughput = queueThroughput.TotalThroughput,
                    DateUTC = queueThroughput.DateUTC
                });

                if (throughputQuery.SupportsHistoricalMetrics)
                {
                    await dataStore.RecordEndpointThroughput(endpoint);
                }
                else
                {
                    await dataStore.AppendEndpointThroughput(endpoint);
                }
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

    private ITimer? backgroundTimer;
}