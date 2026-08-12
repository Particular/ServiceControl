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

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
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
            await Task.Delay(DelayStart, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromDays(1), timeProvider);

            do
            {
                try
                {
                    await GatherThroughput(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to gather throughput from broker");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(BrokerThroughputCollectorHostedService));
        }
    }

    async Task GatherThroughput(CancellationToken cancellationToken)
    {
        logger.LogInformation("Gathering throughput from broker");

        var waitingTasks = new List<Task>();
        var postfixGenerator = new PostfixGenerator();

        await foreach (var queueName in brokerThroughputQuery.GetQueueNames(cancellationToken))
        {
            if (PlatformEndpointHelper.IsPlatformEndpoint(queueName.SanitizedName, throughputSettings))
            {
                continue;
            }

            var postfix = postfixGenerator.GetPostfix(queueName.SanitizedName);
            waitingTasks.Add(Exec(queueName, postfix));
        }

        await Task.WhenAll(waitingTasks);
        await dataStore.SaveBrokerMetadata(new BrokerMetadata(brokerThroughputQuery.ScopeType, brokerThroughputQuery.Data), cancellationToken);
        return;

        async Task Exec(IBrokerQueue queueName, string postfix)
        {
            var endpointId = new EndpointIdentifier(queueName.QueueName, ThroughputSource.Broker);
            var endpoint = await dataStore.GetEndpoint(endpointId, cancellationToken);

            if (endpoint == null)
            {
                endpoint = new Endpoint(endpointId)
                {
                    SanitizedName = queueName.SanitizedName + postfix,
                    Scope = queueName.Scope,
                    EndpointIndicators = [.. queueName.EndpointIndicators]
                };

                await dataStore.SaveEndpoint(endpoint, cancellationToken);
            }

            await foreach (var queueThroughput in brokerThroughputQuery.GetThroughputPerDay(queueName, endpoint.LastCollectedDate.AddDays(1), cancellationToken))
            {
                try
                {
                    await dataStore.RecordEndpointThroughput(queueName.QueueName, ThroughputSource.Broker, queueThroughput.DateUTC, queueThroughput.TotalThroughput, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to record throughput for {QueueName}", queueName.QueueName);
                    throw;
                }
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