namespace Particular.ThroughputCollector.BrokerThroughput;

using System.Collections.Frozen;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Configuration;
using ServiceControl.Transports;

internal class BrokerThroughputCollectorHostedService(
    ILogger<BrokerThroughputCollectorHostedService> logger,
    IThroughputQuery throughputQuery,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    TimeProvider timeProvider)
    : BackgroundService
{
    static readonly string SettingsNamespace = "ThroughputCollector";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"Starting {nameof(BrokerThroughputCollectorHostedService)}");

        throughputQuery.Initialise(LoadBrokerSettingValues(throughputQuery.Settings));

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

    private FrozenDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys) => brokerKeys.ToFrozenDictionary(key => key.Key, key => GetConfigSetting(key.Key));

    string GetConfigSetting(string name) => SettingsReader.Read<string>(new SettingsRootNamespace(SettingsNamespace), name);

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
            var endpointId = new EndpointIdentifier(queueName.QueueName, ThroughputSource.Broker);
            var endpoint = await dataStore.GetEndpoint(endpointId, stoppingToken);
            if (endpoint != null)
            {
                startDate = endpoint.DailyThroughput.Last().DateUTC;
            }

            await foreach (var queueThroughput in throughputQuery.GetThroughputPerDay(queueName, startDate, stoppingToken))
            {
                endpoint = new Endpoint(endpointId)
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
                    await dataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput, stoppingToken);
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
}