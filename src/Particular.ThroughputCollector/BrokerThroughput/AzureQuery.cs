namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;
using Particular.ThroughputCollector.Shared;

public class AzureQuery(ILogger<AzureQuery> logger)
{
    private string serviceBusName = "";
    private string subscriptionId = "";
    private ArmEnvironment environment;
    private ClientSecretCredential? clientCredentials;

    private string? resourceId;

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        settings.TryGetValue(AzureServiceBusSettings.ManagementUrl, out string? managementUrl);
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

        serviceBusName = settings[AzureServiceBusSettings.ServiceBusName];
        subscriptionId = settings[AzureServiceBusSettings.SubscriptionId];
        environment = GetEnvironment();

        clientCredentials = new ClientSecretCredential(settings[AzureServiceBusSettings.TenantId],
            settings[AzureServiceBusSettings.ClientId], settings[AzureServiceBusSettings.ClientSecret]);
    }

    public async IAsyncEnumerable<QueueThroughput> Execute(DateTime startTime, DateTime endTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Connecting to Azure Metrics to get throughput data...");


        await foreach (var queueName in GetQueueNames(cancellationToken))
        {
            logger.LogInformation($"Gathering metrics for \"{queueName}\" queue");

            var metrics = await GetMetrics(queueName, startTime, endTime, cancellationToken);

            var maxThroughput = metrics.Select(timeEntry => timeEntry.Total ?? 0).Max();

            yield return new QueueThroughput(queueName, (long)maxThroughput);
        }
    }

    private async Task<IReadOnlyList<MetricValue>> GetMetrics(string queueName, DateTime startTime, DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var client = new MetricsQueryClient(environment.Endpoint, clientCredentials);

        var response = await client.QueryResourceAsync(resourceId,
            new[] { "CompleteMessage" },
            new MetricsQueryOptions
            {
                Filter = $"EntityName eq '{queueName}'",
                TimeRange = new QueryTimeRange(startTime, endTime),
                Granularity = TimeSpan.FromHours(1)
            },
            cancellationToken);

        var metricValues =
            response.Value.Metrics.FirstOrDefault()?.TimeSeries.FirstOrDefault()?.Values ?? [];

        return metricValues;
    }

    private async IAsyncEnumerable<string> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var armClient = new ArmClient(clientCredentials, subscriptionId,
            new ArmClientOptions { Environment = environment });
        var subscription =
            await armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
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
                    yield return queue.Data.Name;
                }

                yield break;
            }
        }

        throw new Exception($"Could not find a ServiceBus named \"{serviceBusName}\"");
    }
}