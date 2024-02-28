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
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string broker, string serviceControlAPI, string errorQueue, string auditQueue, string transportConnectionString, string persistenceType)
        {
            //For testing only until RavenDB Persistence is working
            persistenceType = "InMemory";

            var services = hostBuilder.Services;
            services.AddSingleton(new ThroughputSettings { Broker = broker, ServiceControlAPI = serviceControlAPI, AuditQueue = auditQueue, ErrorQueue = errorQueue, TransportConnectionString = transportConnectionString, PersistenceType = persistenceType });
            services.AddHostedService<AuditThroughputCollectorHostedService>();
            services.AddHostedService<BrokerThroughputCollectorHostedService>();
            services.AddSingleton<IThroughputCollector, ThroughputCollector>();
            services.AddSingleton<ThroughputController>();

            //TODO add relevant persistence here, based on passed in persistenceType.
            //Will probably also need to initialize it as part of the setup/install process of the instance
            services.AddPersistence(persistenceType);

            return hostBuilder;
        }
    }
}