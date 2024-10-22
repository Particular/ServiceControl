#nullable enable
namespace ServiceControl.Transports.ASBS;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using BrokerThroughput;
using Microsoft.Extensions.Logging;

public class AzureQuery(ILogger<AzureQuery> logger, TimeProvider timeProvider, TransportSettings transportSettings)
    : BrokerThroughputQuery(logger, "AzureServiceBus")
{
    string serviceBusName = string.Empty;
    MetricsQueryClient? client;
    ArmClient? armClient;
    string? resourceId;

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        ConnectionSettings? connectionSettings = ConnectionStringParser.Parse(transportSettings.ConnectionString);
        bool usingManagedIdentity =
            connectionSettings.AuthenticationMethod is TokenCredentialAuthentication;
        Uri? managementUrlParsed = null;
        if (settings.TryGetValue(AzureServiceBusSettings.ManagementUrl, out string? managementUrl))
        {
            if (!Uri.TryCreate(managementUrl, UriKind.Absolute, out managementUrlParsed))
            {
                InitialiseErrors.Add("Management url configuration is invalid");
            }
        }

        if (settings.TryGetValue(AzureServiceBusSettings.ServiceBusName, out string? serviceBusNameValue))
        {
            serviceBusName = serviceBusNameValue.Length == 0 ? string.Empty : serviceBusNameValue;
        }

        if (string.IsNullOrEmpty(serviceBusName))
        {
            // Extract ServiceBus name from connection string
            serviceBusName = ExtractServiceBusName();
            logger.LogInformation("ServiceBus name extracted from connection string");
            Diagnostics.AppendLine($"ServiceBus name not set, defaulted to \"{serviceBusName}\"");
        }
        else
        {
            Diagnostics.AppendLine($"ServiceBus name set to \"{serviceBusName}\"");
        }

        if (!settings.TryGetValue(AzureServiceBusSettings.SubscriptionId, out string? subscriptionId))
        {
            Diagnostics.AppendLine("SubscriptionId not set, using the first found subscription");
        }
        else
        {
            Diagnostics.AppendLine("SubscriptionId set");
        }

        if (!settings.TryGetValue(AzureServiceBusSettings.TenantId, out string? tenantId) && !usingManagedIdentity)
        {
            InitialiseErrors.Add("TenantId is a required setting");
            Diagnostics.AppendLine("TenantId not set");
        }
        else
        {
            Diagnostics.AppendLine("TenantId set");
        }

        if (!settings.TryGetValue(AzureServiceBusSettings.ClientId, out string? clientId) && !usingManagedIdentity)
        {
            InitialiseErrors.Add("ClientId is a required setting");
            Diagnostics.AppendLine("ClientId not set");
        }
        else
        {
            Diagnostics.AppendLine("ClientId set");
        }

        if (!settings.TryGetValue(AzureServiceBusSettings.ClientSecret, out string? clientSecret) &&
            !usingManagedIdentity)
        {
            InitialiseErrors.Add("ClientSecret is a required setting");
            Diagnostics.AppendLine("Client secret not set");
        }
        else
        {
            Diagnostics.AppendLine("Client secret set");
        }

        ArmEnvironment environment = GetEnvironment();

        if (managementUrl == null)
        {
            Diagnostics.AppendLine($"Management Url not set, defaulted to \"{environment.Endpoint}\"");
        }
        else
        {
            Diagnostics.AppendLine($"Management Url set to \"{managementUrl}\"");
        }

        if (InitialiseErrors.Count > 0)
        {
            return;
        }

        TokenCredential clientCredentials;
        if (connectionSettings.AuthenticationMethod is TokenCredentialAuthentication tokenCredentialAuthentication)
        {
            Diagnostics.AppendLine("Attempting to use managed identity");
            clientCredentials = tokenCredentialAuthentication.Credential;
        }
        else
        {
            clientCredentials = new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        client = new MetricsQueryClient(environment.Endpoint, clientCredentials,
            new MetricsQueryClientOptions
            {
                Transport = new HttpClientTransport(
                    new HttpClient(new SocketsHttpHandler
                    {
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                    }))
            });
        armClient = new ArmClient(clientCredentials, subscriptionId,
            new ArmClientOptions
            {
                Environment = environment,
                Transport = new HttpClientTransport(
                    new HttpClient(new SocketsHttpHandler
                    {
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                    }))
            });

        return;

        ArmEnvironment GetEnvironment()
        {
            if (managementUrlParsed == null)
            {
                return ArmEnvironment.AzurePublicCloud;
            }

            if (managementUrlParsed == ArmEnvironment.AzurePublicCloud.Endpoint)
            {
                return ArmEnvironment.AzurePublicCloud;
            }

            if (managementUrlParsed == ArmEnvironment.AzureChina.Endpoint)
            {
                return ArmEnvironment.AzureChina;
            }

            if (managementUrlParsed == ArmEnvironment.AzureGermany.Endpoint)
            {
                return ArmEnvironment.AzureGermany;
            }

            if (managementUrlParsed == ArmEnvironment.AzureGovernment.Endpoint)
            {
                return ArmEnvironment.AzureGovernment;
            }

            string options = string.Join(", ",
                new[]
                {
                    ArmEnvironment.AzurePublicCloud, ArmEnvironment.AzureGermany, ArmEnvironment.AzureGovernment,
                    ArmEnvironment.AzureChina
                }.Select(armEnvironment => $"\"{armEnvironment.Endpoint}\""));
            InitialiseErrors.Add($"Management url configuration is invalid, available options are {options}");

            return ArmEnvironment.AzurePublicCloud;
        }
    }

    public string ExtractServiceBusName()
    {
        const string serviceBusUrlPrefix = "sb://";
        int serviceBusUrlPrefixLength = serviceBusUrlPrefix.Length;
        int startIndex = transportSettings.ConnectionString.IndexOf(serviceBusUrlPrefix, StringComparison.Ordinal);
        if (startIndex == -1)
        {
            startIndex = 0;
        }
        else
        {
            startIndex += serviceBusUrlPrefixLength;
        }

        return transportSettings.ConnectionString.Substring(startIndex,
            transportSettings.ConnectionString.IndexOf('.', startIndex) - startIndex);
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"Gathering metrics for \"{brokerQueue.QueueName}\" queue");

        var endDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        if (endDate < startDate)
        {
            yield break;
        }

        var metrics = await GetMetrics(brokerQueue.QueueName, startDate,
            endDate, cancellationToken);

        DateOnly currentDate = startDate;
        var data = new Dictionary<DateOnly, QueueThroughput>();
        while (currentDate <= endDate)
        {
            data.Add(currentDate, new QueueThroughput { TotalThroughput = 0, DateUTC = currentDate });

            currentDate = currentDate.AddDays(1);
        }

        foreach (var metricValue in metrics)
        {
            data[DateOnly.FromDateTime(metricValue.TimeStamp.UtcDateTime)].TotalThroughput = (long)(metricValue.Total ?? 0);
        }

        foreach (QueueThroughput queueThroughput in data.Values)
        {
            yield return queueThroughput;
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

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SubscriptionResource? subscription = await armClient!.GetDefaultSubscriptionAsync(cancellationToken);
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

    public override string SanitizedEndpointNameCleanser(string endpointName) => endpointName.ToLower();

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(AzureServiceBusSettings.ServiceBusName, AzureServiceBusSettings.ServiceBusNameDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ClientId, AzureServiceBusSettings.ClientIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ClientSecret, AzureServiceBusSettings.ClientSecretDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.TenantId, AzureServiceBusSettings.TenantIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.SubscriptionId, AzureServiceBusSettings.SubscriptionIdDescription),
        new KeyDescriptionPair(AzureServiceBusSettings.ManagementUrl, AzureServiceBusSettings.ManagementUrlDescription)
    ];

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(
        CancellationToken cancellationToken)
    {
        await foreach (IBrokerQueue brokerQueue in GetQueueNames(cancellationToken))
        {
            // Just picking 10 days ago to test the connection
            await foreach (QueueThroughput _ in GetThroughputPerDay(brokerQueue,
                               DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-10),
                               cancellationToken))
            {
                return (true, []);
            }
        }

        return (true, []);
    }

    public static class AzureServiceBusSettings
    {
        public static readonly string ServiceBusName = "ASB/ServiceBusName";
        public static readonly string ServiceBusNameDescription = "Azure Service Bus namespace.";
        public static readonly string ClientId = "ASB/ClientId";
        public static readonly string ClientIdDescription = "ClientId for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
        public static readonly string ClientSecret = "ASB/ClientSecret";
        public static readonly string ClientSecretDescription = "ClientSecret for an Azure login that has access to view metrics data for the Azure Service Bus namespace.";
        public static readonly string TenantId = "ASB/TenantId";
        public static readonly string TenantIdDescription = "Azure Microsoft Extra ID";
        public static readonly string SubscriptionId = "ASB/SubscriptionId";
        public static readonly string SubscriptionIdDescription = "Azure subscription ID";
        public static readonly string ManagementUrl = "ASB/ManagementUrl";
        public static readonly string ManagementUrlDescription = "Azure management URL";
    }
}