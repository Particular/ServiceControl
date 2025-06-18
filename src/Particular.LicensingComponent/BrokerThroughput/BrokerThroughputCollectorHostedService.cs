namespace Particular.LicensingComponent.BrokerThroughput;

using System.Collections.ObjectModel;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Configuration;
using ServiceControl.Transports.BrokerThroughput;
using Shared;

public class BrokerThroughputCollectorHostedService(
    ILogger<BrokerThroughputCollectorHostedService> logger,
    IBrokerThroughputQuery brokerThroughputQuery,
    ThroughputSettings throughputSettings,
    ILicensingDataStore dataStore,
    TimeProvider timeProvider)
    : BackgroundService
{
    public TimeSpan DelayStart { get; set; } = TimeSpan.FromSeconds(40);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        static ReadOnlyDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys)
            => new(brokerKeys.Select(pair => KeyValuePair.Create(pair.Key, SettingsReader.Read<string>(ThroughputSettings.SettingsNamespace, pair.Key)))
                .Where(pair => !string.IsNullOrEmpty(pair.Value)).ToDictionary());

        brokerThroughputQuery.Initialize(LoadBrokerSettingValues(brokerThroughputQuery.Settings));

        if (brokerThroughputQuery.HasInitialisationErrors(out var errorMessage))
        {
            logger.LogError("Could not start {ServiceName}, due to initialisation errors:\n{InitializationErrors}", nameof(BrokerThroughputCollectorHostedService), errorMessage);
            return;
        }

        logger.LogInformation("Starting {ServiceName}", nameof(BrokerThroughputCollectorHostedService));

        try
        {
            await Task.Delay(DelayStart, stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromDays(1), timeProvider);

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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(BrokerThroughputCollectorHostedService));
        }
    }

    async Task GatherThroughput(CancellationToken stoppingToken)
    {
        logger.LogInformation("Gathering throughput from broker");

        var waitingTasks = new List<Task>();
        var postfixGenerator = new PostfixGenerator();

        await foreach (var queueName in brokerThroughputQuery.GetQueueNames(stoppingToken))
        {
            if (PlatformEndpointHelper.IsPlatformEndpoint(queueName.SanitizedName, throughputSettings))
            {
                continue;
            }

            var postfix = postfixGenerator.GetPostfix(queueName.SanitizedName);
            waitingTasks.Add(Exec(queueName, postfix));
        }

        await Task.WhenAll(waitingTasks);
        await dataStore.SaveBrokerMetadata(new BrokerMetadata(brokerThroughputQuery.ScopeType, brokerThroughputQuery.Data), stoppingToken);
        return;

        async Task Exec(IBrokerQueue queueName, string postfix)
        {
            var endpointId = new EndpointIdentifier(queueName.QueueName, ThroughputSource.Broker);
            var endpoint = await dataStore.GetEndpoint(endpointId, stoppingToken);

            if (endpoint == null)
            {
                endpoint = new Endpoint(endpointId)
                {
                    SanitizedName = queueName.SanitizedName + postfix,
                    Scope = queueName.Scope,
                    EndpointIndicators = [.. queueName.EndpointIndicators]
                };

                await dataStore.SaveEndpoint(endpoint, stoppingToken);
            }

            var defaultStartDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-30);
            var startDate = endpoint.LastCollectedDate < defaultStartDate
                ? defaultStartDate
                : endpoint.LastCollectedDate.AddDays(1);

            await foreach (var queueThroughput in brokerThroughputQuery.GetThroughputPerDay(queueName, startDate, stoppingToken))
            {
                await dataStore.RecordEndpointThroughput(queueName.QueueName, ThroughputSource.Broker, queueThroughput.DateUTC, queueThroughput.TotalThroughput, stoppingToken);
            }
        }
    }

    class PostfixGenerator
    {
        readonly Dictionary<string, int> names = new(StringComparer.OrdinalIgnoreCase);

        public string GetPostfix(string sanitizedName)
        {
            if (!names.TryAdd(sanitizedName, 0))
            {
                names[sanitizedName]++;
            }

            return names[sanitizedName] == 0 ? string.Empty : names[sanitizedName].ToString();
        }
    }
}