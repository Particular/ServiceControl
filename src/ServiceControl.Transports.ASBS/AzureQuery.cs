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
using Azure.ResourceManager;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Monitor.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using BrokerThroughput;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;

public class AzureQuery(ILogger<AzureQuery> logger, TimeProvider timeProvider, TransportSettings transportSettings)
    : BrokerThroughputQuery(logger, "AzureServiceBus")
{
    const string CompleteMessageMetricName = "CompleteMessage";
    const string MicrosoftServicebusNamespacesMetricsNamespace = "Microsoft.ServiceBus/Namespaces";

    // ASB keeps 90 days of data but will only return 30 days in a single query
    const int MaxDaysToCollect = 90;
    const int MaxDaysToCollectInOneQuery = 30;

    string serviceBusName = string.Empty;
    ArmClient? armClient;
    TokenCredential? credential;
    ResourceIdentifier? resourceId;
    ArmEnvironment armEnvironment;

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
            logger.LogInformation("Azure Service Bus namespace name extracted from connection string");
            Diagnostics.AppendLine($"Azure Service Bus namespace not set, defaulted to \"{serviceBusName}\"");
        }
        else
        {
            Diagnostics.AppendLine($"Azure Service Bus namespace set to \"{serviceBusName}\"");
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

        armEnvironment = GetEnvironment();

        if (managementUrl == null)
        {
            Diagnostics.AppendLine($"Management Url not set, defaulted to \"{armEnvironment.Endpoint}\"");
        }
        else
        {
            Diagnostics.AppendLine($"Management Url set to \"{managementUrl}\"");
        }

        if (InitialiseErrors.Count > 0)
        {
            return;
        }

        if (connectionSettings.AuthenticationMethod is TokenCredentialAuthentication tokenCredentialAuthentication)
        {
            Diagnostics.AppendLine("Attempting to use managed identity");
            credential = tokenCredentialAuthentication.Credential;
        }
        else
        {
            credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        armClient = new ArmClient(credential, subscriptionId,
            new ArmClientOptions
            {
                Environment = armEnvironment,
                Transport = new HttpClientTransport(
                    new HttpClient(new SocketsHttpHandler
                    {
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                    }))
            });

        return;

        ArmEnvironment GetEnvironment()
        {
            if (managementUrlParsed == null || managementUrlParsed == ArmEnvironment.AzurePublicCloud.Endpoint)
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
                    ArmEnvironment.AzurePublicCloud, ArmEnvironment.AzureGermany, ArmEnvironment.AzureGovernment, ArmEnvironment.AzureChina
                }.Select(environment => $"\"{environment.Endpoint}\""));
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
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger.LogInformation($"Gathering metrics for \"{brokerQueue.QueueName}\" queue");

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime);
        var earliestStartDate = today.AddDays(-MaxDaysToCollect);
        if (startDate < earliestStartDate)
        {
            startDate = earliestStartDate;
        }

        // Collect up to yesterday, as today's data is incomplete
        // We will collect today's data tomorrow
        var endDate = today.AddDays(-1);
        if (endDate < startDate)
        {
            yield break;
        }

        DateOnly currentDate = startDate;
        var data = new Dictionary<DateOnly, QueueThroughput>();
        while (currentDate <= endDate)
        {
            data.Add(currentDate, new QueueThroughput
            {
                TotalThroughput = 0,
                DateUTC = currentDate
            });
            currentDate = currentDate.AddDays(1);
        }

        foreach (var (periodStart, periodEnd) in ReportingWindow.GetReportingWindow(startDate, endDate, MaxDaysToCollectInOneQuery))
        {
            var metrics = await GetMetrics(brokerQueue.QueueName, periodStart, periodEnd, cancellationToken);

            foreach (var metricValue in metrics)
            {
                data[DateOnly.FromDateTime(metricValue.TimeStamp.UtcDateTime)].TotalThroughput = (long)(metricValue.Total ?? 0);
            }
        }

        foreach (QueueThroughput queueThroughput in data.Values)
        {
            yield return queueThroughput;
        }
    }

    async Task<IReadOnlyList<MonitorMetricValue>> GetMetrics(string queueName, DateOnly startTime, DateOnly endTime,
        CancellationToken cancellationToken = default)
    {
        var options = new ArmResourceGetMonitorMetricsOptions()
        {
            Metricnames = CompleteMessageMetricName,
            Timespan = $"{startTime.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc):o}/{endTime.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc):o}",
            Filter = $"EntityName eq '{queueName}'",
            Interval = TimeSpan.FromDays(1),
            Metricnamespace = MicrosoftServicebusNamespacesMetricsNamespace
        };

        var response = armClient.GetMonitorMetricsAsync(resourceId, options, cancellationToken);

        var metric = await response.SingleOrDefaultAsync(m => m.Name.Value == CompleteMessageMetricName, cancellationToken)
            ?? throw new Exception($"Metric {CompleteMessageMetricName} not found for {queueName}");

        var timeSeries = metric.Timeseries.SingleOrDefault()
            ?? throw new Exception($"Metric {metric.Name.Value} for {queueName} contains no time series");

        return timeSeries.Data;
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var validNamespaces = await GetValidNamespaceNames(cancellationToken);

        SubscriptionResource? subscription = await armClient!.GetDefaultSubscriptionAsync(cancellationToken);
        var namespaces = subscription.GetServiceBusNamespacesAsync(cancellationToken);

        await foreach (var serviceBusNamespaceResource in namespaces)
        {
            if (!validNamespaces.Contains(serviceBusNamespaceResource.Data.Name))
            {
                continue;
            }

            resourceId = serviceBusNamespaceResource.Id;

            await foreach (var queue in serviceBusNamespaceResource.GetServiceBusQueues()
                               .WithCancellation(cancellationToken))
            {
                yield return new DefaultBrokerQueue(queue.Data.Name);
            }

            yield break;
        }

        throw new Exception($"Could not find a Azure Service Bus namespace named \"{serviceBusName}\"");
    }

    // ArmEnvironment Audience Values: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/resourcemanager/Azure.ResourceManager/src/ArmEnvironment.cs
    // Service Bus Domains: https://learn.microsoft.com/en-us/rest/api/servicebus/
    static readonly Dictionary<ArmEnvironment, string> ServiceBusDomains = new()
    {
        { ArmEnvironment.AzurePublicCloud, "servicebus.windows.net" },
        { ArmEnvironment.AzureGovernment, "servicebus.usgovcloudapi.net" },
        { ArmEnvironment.AzureGermany, "servicebus.cloudapi.de" },
        { ArmEnvironment.AzureChina, "servicebus.chinacloudapi.cn" },
    };

    async Task<HashSet<string>> GetValidNamespaceNames(CancellationToken cancellationToken = default)
    {
        var validNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { serviceBusName };

        var serviceBusCloudDomain = ServiceBusDomains.GetValueOrDefault(armEnvironment, "servicebus.windows.net");

        // Worst case: the DNS lookup finds nothing additional to match
        var queryDomain = $"{serviceBusName}.{serviceBusCloudDomain}";
        var validDomainTail = $".{serviceBusCloudDomain}.";

        var dnsLookup = new LookupClient();
        var dnsResult = await dnsLookup.QueryAsync(queryDomain, QueryType.CNAME, cancellationToken: cancellationToken);
        var domain = (dnsResult.Answers.FirstOrDefault() as CNameRecord)?.CanonicalName.Value;
        if (domain is not null && domain.EndsWith(validDomainTail))
        {
            // In some cases, like private networking access, result might be something like `namespacename.private` with a dot in the middle
            // which is not a big deal because that will not actually match a namespace name in metrics
            var otherName = domain[..^validDomainTail.Length];
            validNamespaces.Add(otherName);
        }

        return validNamespaces;
    }

    public override string SanitizedEndpointNameCleanser(string endpointName) => endpointName.ToLower();

    public override KeyDescriptionPair[] Settings =>
    [
        new(AzureServiceBusSettings.ServiceBusName, AzureServiceBusSettings.ServiceBusNameDescription),
        new(AzureServiceBusSettings.ClientId, AzureServiceBusSettings.ClientIdDescription),
        new(AzureServiceBusSettings.ClientSecret, AzureServiceBusSettings.ClientSecretDescription),
        new(AzureServiceBusSettings.TenantId, AzureServiceBusSettings.TenantIdDescription),
        new(AzureServiceBusSettings.SubscriptionId, AzureServiceBusSettings.SubscriptionIdDescription),
        new(AzureServiceBusSettings.ManagementUrl, AzureServiceBusSettings.ManagementUrlDescription)
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
