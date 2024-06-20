namespace Particular.LicensingComponent;

using System.Collections.Frozen;
using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MonitoringThroughput;
using ServiceControl.Configuration;
using ServiceControl.Transports.BrokerThroughput;

public static class LicensingComponentHostBuilderExtensions
{
    public static IHostApplicationBuilder AddLicensingComponent(this IHostApplicationBuilder hostBuilder, string transportType, string errorQueue, string serviceControlQueue, string customerName, string serviceControlVersion)
    {
        var services = hostBuilder.Services;

        var throughputSettings = new ThroughputSettings(serviceControlQueue, errorQueue, transportType, customerName, serviceControlVersion);
        services.AddSingleton(throughputSettings);
        services.AddHostedService<AuditThroughputCollectorHostedService>();
        services.AddSingleton<IThroughputCollector, ThroughputCollector>();
        services.AddSingleton<IAuditQuery, AuditQuery>();
        services.AddSingleton<MonitoringService>();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IBrokerThroughputQuery)))
        {
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
            services.AddSingleton(provider =>
            {
                var queryProvider = provider.GetRequiredService<IBrokerThroughputQuery>();
                queryProvider.Initialise(LoadBrokerSettingValues(queryProvider.Settings));

                return queryProvider;
            });
        }

        return hostBuilder;

        static FrozenDictionary<string, string> LoadBrokerSettingValues(IEnumerable<KeyDescriptionPair> brokerKeys)
        {
            return brokerKeys.Select(pair => KeyValuePair.Create(pair.Key, SettingsReader.Read<string>(ThroughputSettings.SettingsNamespace, pair.Key)))
                .Where(pair => !string.IsNullOrEmpty(pair.Value)).ToFrozenDictionary(key => key.Key, key => key.Value);
        }
    }
}