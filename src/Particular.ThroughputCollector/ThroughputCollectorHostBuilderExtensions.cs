namespace Particular.ThroughputCollector;

using System.Collections.Frozen;
using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector.Shared;
using ServiceControl.Configuration;
using ServiceControl.Transports;

public static class ThroughputCollectorHostBuilderExtensions
{
    public static IHostApplicationBuilder AddThroughputCollector(
        this IHostApplicationBuilder hostBuilder,
        string transportType,
        string errorQueue,
        string serviceControlQueue,
        string persistenceType,
        string persistenceAssembly,
        string customerName,
        string serviceControlVersion,
        Type? throughputQueryProvider)
    {
        var services = hostBuilder.Services;

        var broker = Broker.None;
        switch (transportType)
        {
            case "NetStandardAzureServiceBus":
                broker = Broker.AzureServiceBus;
                break;
            case "RabbitMQ.ClassicConventionalRouting":
            case "RabbitMQ.ClassicDirectRouting":
            case "RabbitMQ.QuorumConventionalRouting":
            case "RabbitMQ.QuorumDirectRouting":
            case "RabbitMQ.ConventionalRouting":
            case "RabbitMQ.DirectRouting":
                broker = Broker.RabbitMQ;
                break;
            case "SQLServer":
                broker = Broker.SqlServer;
                break;
            case "AmazonSQS":
                broker = Broker.AmazonSQS;
                break;
        }

        var throughputSettings = new ThroughputSettings(broker, serviceControlQueue, errorQueue, persistenceType, transportType, customerName, serviceControlVersion);
        services.AddSingleton(throughputSettings);
        services.AddHostedService<AuditThroughputCollectorHostedService>();
        services.AddSingleton<IThroughputCollector, ThroughputCollector>();
        services.AddSingleton<AuditQuery>();

        if (broker != Broker.None)
        {
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
        }

        hostBuilder.AddThroughputCollectorPersistence(persistenceType, persistenceAssembly);

        if (throughputQueryProvider != null)
        {
            services.AddSingleton(throughputQueryProvider);
            services.AddSingleton(provider =>
            {
                var queryProvider = (IBrokerThroughputQuery)provider.GetRequiredService(throughputQueryProvider);
                queryProvider.Initialise(LoadBrokerSettingValues(queryProvider.Settings));

                return queryProvider;
            });
        }

        return hostBuilder;

        static FrozenDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys) =>
            brokerKeys.Select(pair => KeyValuePair.Create(pair.Key, SettingsReader.Read<string>(new SettingsRootNamespace(PlatformEndpointHelper.SettingsNamespace), pair.Key)))
                .Where(pair => !string.IsNullOrEmpty(pair.Value)).ToFrozenDictionary(key => key.Key, key => key.Value);
    }

    public static IHostApplicationBuilder AddThroughputCollectorPersistence(this IHostApplicationBuilder hostBuilder, string persistenceType, string persistenceAssembly)
    {
        hostBuilder.Services.AddPersistence(persistenceType, persistenceAssembly);

        return hostBuilder;
    }
}