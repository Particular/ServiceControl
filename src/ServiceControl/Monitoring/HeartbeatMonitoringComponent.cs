namespace ServiceControl.Monitoring
{
    using Connection;
    using EndpointControl.Handlers;
    using EventLog;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class HeartbeatMonitoringComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddHostedService<HeartbeatMonitoringHostedService>();
                collection.AddSingleton<IEndpointInstanceMonitoring, EndpointInstanceMonitoring>();

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
            // TODO: Move this in the persister project
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                var connectionString = SettingsReader<string>.Read("SqlStorageConnectionString");
                context.RegisterInstallationTask(() => SetupSqlPersistence.SetupMonitoring(connectionString));
            }
        }
    }
}