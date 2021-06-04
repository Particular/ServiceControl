namespace ServiceControl.Monitoring
{
    using EndpointControl.Handlers;
    using Infrastructure.DomainEvents;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Operations;

    static class HeartbeatMonitoringHostBuilderExtensions
    {
        public static IHostBuilder UseHeartbeatMonitoring(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<IDataMigration, PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicateDataMigration>();
                collection.AddHostedService<HeartbeatMonitoringHostedService>();
                collection.AddSingleton<EndpointInstanceMonitoring>();
                collection.AddSingleton<MonitoringDataStore>();
                collection.AddSingleton<IEnrichImportedErrorMessages, DetectNewEndpointsFromErrorImportsEnricher>();
                collection.AddDomainEventHandler<MonitoringDataPersister>();
            });
            return hostBuilder;
        }
    }
}