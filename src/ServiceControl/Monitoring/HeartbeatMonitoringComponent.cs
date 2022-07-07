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

                switch (settings.DataStoreType)
                {
                    case DataStoreType.InMemory:
                        collection.AddSingleton<IMonitoringDataStore, InMemoryMonitoringDataStore>();
                        break;
                    case DataStoreType.RavenDb:
                        collection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
                        break;
                    case DataStoreType.SqlDb:
                        collection.AddSingleton<IMonitoringDataStore>(sp => new SqlDbMonitoringDataStore(settings.SqlStorageConnectionString));
                        break;
                    default:
                        break;
                }

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
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                context.RegisterSetupTask(() => SqlDbMonitoringDataStore.Setup(settings.SqlStorageConnectionString));
            }
        }
    }
}