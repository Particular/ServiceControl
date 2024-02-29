namespace Particular.ThroughputCollector
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ThroughputCollector.Audit;
    using Particular.ThroughputCollector.Broker;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.WebApi;

    public static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string serviceControlAPI, string errorQueue, string auditQueue, string transportConnectionString, string persistenceType)
        {
            //For testing only until RavenDB Persistence is working
            persistenceType = "InMemory";

            var services = hostBuilder.Services;

            var broker = Contracts.Broker.None;
            switch (transportType)
            {
                case "NetStandardAzureServiceBus":
                    broker = Contracts.Broker.AzureServiceBus;
                    break;
                case "AzureStorageQueue":
                    broker = Contracts.Broker.AzureStorageQueues;
                    break;
                case "RabbitMQ.ClassicConventionalRouting":
                case "RabbitMQ.ClassicDirectRouting":
                case "RabbitMQ.QuorumConventionalRouting":
                case "RabbitMQ.QuorumDirectRouting":
                case "RabbitMQ.ConventionalRouting":
                case "RabbitMQ.DirectRouting":
                    broker = Contracts.Broker.RabbitMQ;
                    break;
                case "SQLServer":
                    broker = Contracts.Broker.SqlServer;
                    break;
                case "AmazonSQS":
                    broker = Contracts.Broker.AmazonSQS;
                    break;
                default:
                    break;
            }

            services.AddSingleton(new ThroughputSettings { Broker = broker, ServiceControlAPI = serviceControlAPI, AuditQueue = auditQueue, ErrorQueue = errorQueue, TransportConnectionString = transportConnectionString, PersistenceType = persistenceType });
            //TODO could get ILoggerFactory loggerFactory here and create the one for throughput collector and inject the ilogger into the services - that way won't have to create it every time.
            services.AddHostedService<AuditThroughputCollectorHostedService>();
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
            services.AddSingleton<IThroughputCollector, ThroughputCollector>();
            services.AddSingleton<ThroughputController>();

            services.AddPersistence(persistenceType);

            return hostBuilder;
        }
    }
}