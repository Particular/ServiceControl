namespace Particular.ThroughputCollector;

using AuditThroughput;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebApi;

public static class ThroughputCollectorHostBuilderExtensions
{
    public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string errorQueue, string serviceControlQueue, string auditQueue, string persistenceType, string customerName, string serviceControlVersion)
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

        services.AddSingleton(new ThroughputSettings(broker, serviceControlQueue, errorQueue, persistenceType, customerName, serviceControlVersion, auditQueue));
        services.AddHostedService<AuditThroughputCollectorHostedService>();
        services.AddSingleton<IThroughputCollector, ThroughputCollector>();
        services.AddSingleton<ThroughputController>();

        if (broker != Broker.None)
        {
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
        }

        hostBuilder.AddThroughputCollectorPersistence(persistenceType);

        return hostBuilder;
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