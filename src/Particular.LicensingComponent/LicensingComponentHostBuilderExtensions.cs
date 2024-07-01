namespace Particular.LicensingComponent;

using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MonitoringThroughput;
using ServiceControl.Transports.BrokerThroughput;

public static class LicensingComponentHostBuilderExtensions
{
    public static IHostApplicationBuilder AddLicensingComponent(this IHostApplicationBuilder hostBuilder, string transportType, string errorQueue, string serviceControlQueue, string customerName, string serviceControlVersion)
    {
        var services = hostBuilder.Services;

        var throughputSettings = new ThroughputSettings(serviceControlQueue, errorQueue, transportType, customerName, serviceControlVersion);
        services.AddSingleton(throughputSettings);
        services.AddHostedService<AuditThroughputCollectorHostedService>();
        services.AddHostedService<MonitoringThroughputHostedService>();
        services.AddSingleton<IThroughputCollector, ThroughputCollector>();
        services.AddSingleton<IAuditQuery, AuditQuery>();
        services.AddSingleton<MonitoringService>();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IBrokerThroughputQuery)))
        {
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
        }

        return hostBuilder;
    }
}