namespace Particular.License
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.License.Contracts;
    using Particular.License.Throughput.Audit;
    using Particular.License.Throughput.Broker;

    public static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string broker, string serviceControlAPI, string errorQueue, string auditQueue, string transportConnectionString, string persistenceType)
        {
            var services = hostBuilder.Services;
            services.AddSingleton(new PlatformData { Broker = broker, ServiceControlAPI = serviceControlAPI, AuditQueue = auditQueue, ErrorQueue = errorQueue, TransportConnectionString = transportConnectionString, PersistenceType = persistenceType });
            services.AddHostedService<AuditThroughputCollectorHostedService>();
            services.AddHostedService<BrokerThroughputCollectorHostedService>();

            //TODO add relevant persistence here, based on passed in persistenceType.
            //Will probably also need to initialize it as part of the setup/install process of the instance

            return hostBuilder;
        }
    }
}