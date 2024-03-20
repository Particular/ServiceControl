namespace Particular.ThroughputCollector
{
    using Audit;
    using Broker;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ThroughputCollector.Infrastructure;
    using WebApi;

    public static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string serviceControlAPI, string errorQueue, string serviceControlQueue, string auditQueue, string transportConnectionString, string persistenceType, string customerName, string serviceControlVersion)
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
            }

            //try to ensure the serviceControlAPI is correct (https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0#url-formats)
            if (serviceControlAPI.StartsWith("http://*"))
            {
                serviceControlAPI = serviceControlAPI.Replace("http://*", "http://localhost");
            }
            else if (serviceControlAPI.StartsWith("http://0.0.0.0"))
            {
                serviceControlAPI = serviceControlAPI.Replace("http://0.0.0.0", "http://localhost");
            }

            services.AddSingleton(new ThroughputSettings(broker: broker, transportConnectionString: transportConnectionString, serviceControlAPI: serviceControlAPI, serviceControlQueue: serviceControlQueue, errorQueue: errorQueue, persistenceType: persistenceType, customerName: customerName, serviceControlVersion: serviceControlVersion, auditQueue: auditQueue));
            services.AddHostedService<AuditThroughputCollectorHostedService>();
            services.AddSingleton<IThroughputCollector, ThroughputCollector>();
            services.AddSingleton<ThroughputController>();
            switch (broker)
            {
                case Contracts.Broker.AmazonSQS:
                    services.AddSingleton<IThroughputQuery, AmazonSQSQuery>();
                    services.AddHostedService<BrokerThroughputCollectorHostedService>();
                    break;
                case Contracts.Broker.RabbitMQ:
                    services.AddSingleton<IThroughputQuery, RabbitMQQuery>();
                    services.AddHostedService<BrokerThroughputCollectorHostedService>();
                    break;
                case Contracts.Broker.AzureServiceBus:
                    services.AddSingleton<IThroughputQuery, AzureQuery>();
                    services.AddHostedService<BrokerThroughputCollectorHostedService>();
                    break;
                case Contracts.Broker.SqlServer:
                    services.AddSingleton<IThroughputQuery, SqlServerQuery>();
                    services.AddHostedService<BrokerThroughputCollectorHostedService>();
                    break;
            }

            services.AddPersistence(persistenceType);

            return hostBuilder;
        }
    }
}