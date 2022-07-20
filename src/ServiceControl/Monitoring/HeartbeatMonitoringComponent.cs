namespace ServiceControl.Monitoring
{
    using Connection;
    using EndpointControl.Handlers;
    using EventLog;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Recoverability;
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

                collection.AddDomainEventHandler<MonitoringDataPersister>();

                collection.AddEventLogMapping<EndpointFailedToHeartbeatDefinition>();
                collection.AddEventLogMapping<HeartbeatingEndpointDetectedDefinition>();
                collection.AddEventLogMapping<EndpointHeartbeatRestoredDefinition>();
                collection.AddEventLogMapping<EndpointStartedDefinition>();
                collection.AddEventLogMapping<KnownEndpointUpdatedDefinition>();

                collection.AddIntegrationEventPublisher<HeartbeatRestoredPublisher>();
                collection.AddIntegrationEventPublisher<HeartbeatStoppedPublisher>();

                collection.AddErrorMessageEnricher<DetectNewEndpointsFromErrorImportsEnricher>();

                collection.AddPlatformConnectionProvider<HeartbeatsPlatformConnectionDetailsProvider>();
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
            //TODO can we do this dynamically somehow?
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                context.RegisterInstallationTask(() => Persistence.SetupSqlPersistence.SetupMonitoring(settings.SqlStorageConnectionString));
            }
        }
    }
}