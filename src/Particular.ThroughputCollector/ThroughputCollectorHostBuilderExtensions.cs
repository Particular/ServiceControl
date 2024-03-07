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
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string transportType, string serviceControlAPI, string errorQueue, string serviceControlQueue, string auditQueue, string transportConnectionString, string persistenceType)
        {
            //For testing only until RavenDB Persistence is working
            persistenceType = "InMemory";

            var services = hostBuilder.Services;

            var broker = Contracts.Broker.ServiceControl;
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

            services.AddSingleton(new ThroughputSettings { Broker = broker, ServiceControlAPI = serviceControlAPI, ServiceControlQueue = serviceControlQueue, AuditQueue = auditQueue, ErrorQueue = errorQueue, TransportConnectionString = transportConnectionString, PersistenceType = persistenceType });
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