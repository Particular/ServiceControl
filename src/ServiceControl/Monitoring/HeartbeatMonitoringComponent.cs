namespace ServiceControl.Monitoring
{
    using Connection;
    using EndpointControl.Handlers;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Operations;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class HeartbeatMonitoringComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<IDataMigration, PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicateDataMigration>();
                collection.AddHostedService<HeartbeatMonitoringHostedService>();
                collection.AddSingleton<EndpointInstanceMonitoring>();
                collection.AddSingleton<MonitoringDataStore>();
                collection.AddSingleton<IEnrichImportedErrorMessages, DetectNewEndpointsFromErrorImportsEnricher>();
                collection.AddDomainEventHandler<MonitoringDataPersister>();

                collection.AddIntegrationEventPublisher<HeartbeatRestoredPublisher>();
                collection.AddIntegrationEventPublisher<HeartbeatStoppedPublisher>();

                collection.AddPlatformConnectionProvider<HeartbeatsPlatformConnectionDetailsProvider>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
        }
    }
}