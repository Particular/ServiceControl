namespace Particular.ThroughputCollector;

using System.Collections.Frozen;
using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector.MonitoringThroughput;
using ServiceControl.Configuration;
using ServiceControl.Transports;

public static class ThroughputCollectorHostBuilderExtensions
{
    public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string errorQueue, string serviceControlQueue, string persistenceType, string customerName, string serviceControlVersion, Type? throughputQueryProvider)
    {
        //For testing only until RavenDB Persistence is working
        persistenceType = "InMemory";

        var services = hostBuilder.Services;

        var throughputSettings = new ThroughputSettings(serviceControlQueue, errorQueue, persistenceType, transportType, customerName, serviceControlVersion);
        services.AddSingleton(throughputSettings);
        services.AddHostedService<AuditThroughputCollectorHostedService>();
        services.AddSingleton<IThroughputCollector, ThroughputCollector>();
        services.AddSingleton<IAuditQuery, AuditQuery>();
        services.AddSingleton<MonitoringService>();

        hostBuilder.AddThroughputCollectorPersistence(persistenceType);

        if (throughputQueryProvider != null)
        {
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
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
            brokerKeys.Select(pair => KeyValuePair.Create(pair.Key, SettingsReader.Read<string>(ThroughputSettings.SettingsNamespace, pair.Key)))
                .Where(pair => !string.IsNullOrEmpty(pair.Value)).ToFrozenDictionary(key => key.Key, key => key.Value);
    }

    public static IHostApplicationBuilder AddThroughputCollectorPersistence(this IHostApplicationBuilder hostBuilder, string persistenceType)
    {
        hostBuilder.Services.AddPersistence(persistenceType);

        return hostBuilder;
    }
}