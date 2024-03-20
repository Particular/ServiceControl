namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;
using Shared;

public class AzureQuery(ILogger<AzureQuery> logger) : IThroughputQuery, IBrokerInfo
{
    private string serviceBusName = "";
    private MetricsQueryClient? client;
    private ArmClient? armClient;
    private string? resourceId;

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        settings.TryGetValue(AzureServiceBusSettings.ManagementUrl, out var managementUrl);

        serviceBusName = settings[AzureServiceBusSettings.ServiceBusName];

        var subscriptionId = settings[AzureServiceBusSettings.SubscriptionId];
        var environment = GetEnvironment();
        var clientCredentials = new ClientSecretCredential(settings[AzureServiceBusSettings.TenantId],
            settings[AzureServiceBusSettings.ClientId], settings[AzureServiceBusSettings.ClientSecret]);

        client = new MetricsQueryClient(environment.Endpoint, clientCredentials, new MetricsQueryClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(new SocketsHttpHandler { PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2) }))
        });
        armClient = new ArmClient(clientCredentials, subscriptionId,
            new ArmClientOptions { Environment = environment, Transport = new HttpClientTransport(new HttpClient(new SocketsHttpHandler { PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2) })) });
        return;

        ArmEnvironment GetEnvironment()
        {
            if (managementUrl == null)
            {
                return ArmEnvironment.AzurePublicCloud;
            }

            if (managementUrl.Equals(ArmEnvironment.AzureChina.Endpoint.ToString(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return ArmEnvironment.AzureChina;
            }

            if (managementUrl.Equals(ArmEnvironment.AzureGermany.Endpoint.ToString(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return ArmEnvironment.AzureGermany;
            }

            if (managementUrl.Equals(ArmEnvironment.AzureGovernment.Endpoint.ToString(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return ArmEnvironment.AzureGovernment;
            }

            return ArmEnvironment.AzurePublicCloud;
        }
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"Gathering metrics for \"{queueName}\" queue");

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        if (endDate <= startDate)
        {
            yield break;
        }

        var metrics = await GetMetrics(queueName.QueueName, startDate,
            endDate, cancellationToken);

        foreach (var metricValue in metrics)
        {
            yield return new QueueThroughput
            {
                TotalThroughput = (long)(metricValue.Total ?? 0),
                DateUTC = DateOnly.FromDateTime(metricValue.TimeStamp.UtcDateTime)
            };
        }
    }

    private async Task<IReadOnlyList<MetricValue>> GetMetrics(string queueName, DateOnly startTime, DateOnly endTime,
        CancellationToken cancellationToken = default)
    {
        var response = await client!.QueryResourceAsync(resourceId,
            new[] { "CompleteMessage" },
            new MetricsQueryOptions
            {
                Filter = $"EntityName eq '{queueName}'",
                TimeRange = new QueryTimeRange(startTime.ToDateTime(TimeOnly.MinValue), endTime.ToDateTime(TimeOnly.MinValue)),
                Granularity = TimeSpan.FromDays(1)
            },
            cancellationToken);

        var metricValues =
            response.Value.Metrics.FirstOrDefault()?.TimeSeries.FirstOrDefault()?.Values ?? [];

        return metricValues;
    }

    public async IAsyncEnumerable<IQueueName> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscription = await armClient!.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
        var namespaces =
            subscription.GetServiceBusNamespacesAsync(cancellationToken);

        await foreach (var serviceBusNamespaceResource in namespaces.WithCancellation(
                           cancellationToken))
        {
            if (serviceBusNamespaceResource.Data.Name == serviceBusName)
            {
                resourceId = serviceBusNamespaceResource.Id;
                await foreach (var queue in serviceBusNamespaceResource.GetServiceBusQueues()
                                   .WithCancellation(cancellationToken))
                {
                    yield return new DefaultQueueName(queue.Data.Name);
                }

                yield break;
            }
        }

        throw new Exception($"Could not find a ServiceBus named \"{serviceBusName}\"");
    }

    public string? ScopeType { get; }

    public bool SupportsHistoricalMetrics => true;
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "AzureServiceBus";
}