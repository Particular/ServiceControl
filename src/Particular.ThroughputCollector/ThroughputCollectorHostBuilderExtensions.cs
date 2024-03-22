namespace Particular.ThroughputCollector;

using System.Collections.Frozen;
using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Configuration;
using ServiceControl.Transports;

public static class ThroughputCollectorHostBuilderExtensions
{
    static readonly string SettingsNamespace = "ThroughputCollector";

    public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string errorQueue, string serviceControlQueue, string persistenceType, string customerName, string serviceControlVersion, Type? throughputQueryProvider)
    {
        //For testing only until RavenDB Persistence is working
        persistenceType = "InMemory";

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

        hostBuilder.AddThroughputCollectorPersistence(persistenceType, errorQueue, serviceControlQueue);

        if (throughputQueryProvider != null)
        {
            services.AddSingleton(throughputQueryProvider);
            services.AddSingleton<IBrokerThroughputQuery>(provider =>
            {
                var queryProvider = (IBrokerThroughputQuery)provider.GetRequiredService(throughputQueryProvider);
                queryProvider.Initialise(LoadBrokerSettingValues(queryProvider.Settings));

                return queryProvider;
            });
        }

        return hostBuilder;

        static FrozenDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys)
        {
            return brokerKeys.ToFrozenDictionary(key => key.Key, key => SettingsReader.Read<string>(new SettingsRootNamespace(SettingsNamespace), key.Key));
        }
    }

    public static IHostApplicationBuilder AddThroughputCollectorPersistence(this IHostApplicationBuilder hostBuilder, string persistenceType, string? errorQueue = null, string? serviceControlQueue = null)
    {
        hostBuilder.Services.AddPersistence(persistenceType, settings =>
        {
            if (!string.IsNullOrEmpty(errorQueue))
            {
                settings.PlatformEndpointNames.Add(errorQueue);
            }

            if (!string.IsNullOrEmpty(serviceControlQueue))
            {
                settings.PlatformEndpointNames.Add(serviceControlQueue);
            }
        });

        return hostBuilder;
    }
}