#nullable enable
namespace ServiceControl.Transports.ASBS;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

public class AzureQuery(ILogger<AzureQuery> logger, TimeProvider timeProvider) : IThroughputQuery
{
    string serviceBusName = "";
    MetricsQueryClient? client;
    ArmClient? armClient;
    string? resourceId;

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

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"Gathering metrics for \"{brokerQueue}\" queue");

        var endDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        if (endDate < startDate)
        {
            yield break;
        }

        var metrics = await GetMetrics(brokerQueue.QueueName, startDate,
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

    async Task<IReadOnlyList<MetricValue>> GetMetrics(string queueName, DateOnly startTime, DateOnly endTime,
        CancellationToken cancellationToken = default)
    {
        var response = await client!.QueryResourceAsync(resourceId,
            new[] { "CompleteMessage" },
            new MetricsQueryOptions
            {
                Filter = $"EntityName eq '{queueName}'",
                TimeRange = new QueryTimeRange(startTime.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), endTime.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
                Granularity = TimeSpan.FromDays(1)
            },
            cancellationToken);

        var metricValues =
            response.Value.Metrics.FirstOrDefault()?.TimeSeries.FirstOrDefault()?.Values ?? [];

        return metricValues;
    }

    public async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
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
                    yield return new DefaultBrokerQueue(queue.Data.Name);
                }

                yield break;
            }
        }

        throw new Exception($"Could not find a ServiceBus named \"{serviceBusName}\"");
    }

    public string? ScopeType { get; }

    public bool SupportsHistoricalMetrics => true;
    public KeyDescriptionPair[] Settings => [
        new KeyDescriptionPair(AzureServiceBusSettings.ServiceBusName, AzureServiceBusSettings.ServiceBusNameDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ClientId, AzureServiceBusSettings.ClientIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ClientSecret, AzureServiceBusSettings.ClientSecretDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.TenantId, AzureServiceBusSettings.TenantIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.SubscriptionId, AzureServiceBusSettings.SubscriptionIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ManagementUrl, AzureServiceBusSettings.ManagementUrlDescription)
    ];
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "AzureServiceBus";

    public static class AzureServiceBusSettings
    {
        public static readonly string ServiceBusName = "ASB/ServiceBusName";
        public static readonly string ServiceBusNameDescription = "Azure Service Bus namespace to view metrics.";
        public static readonly string ClientId = "ASB/ClientId";
        public static readonly string ClientIdDescription = "ClientId for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
        public static readonly string ClientSecret = "ASB/ClientSecret";
        public static readonly string ClientSecretDescription = "ClientSecret for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
        public static readonly string TenantId = "ASB/TenantId";
        public static readonly string TenantIdDescription = "??";
        public static readonly string SubscriptionId = "ASB/SubscriptionId";
        public static readonly string SubscriptionIdDescription = "??";
        public static readonly string ManagementUrl = "ASB/ManagementUrl";
        public static readonly string ManagementUrlDescription = "??";
    }
}