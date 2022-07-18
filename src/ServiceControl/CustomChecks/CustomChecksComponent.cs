namespace ServiceControl.CustomChecks
{
    using Connection;
    using Contracts.Operations;
    using Dapper;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class CustomChecksComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                serviceCollection.AddHostedService<CustomChecksHostedService>();
                serviceCollection.AddIntegrationEventPublisher<CustomCheckFailedPublisher>();
                serviceCollection.AddIntegrationEventPublisher<CustomCheckSucceededPublisher>();
                serviceCollection.AddEventLogMapping<CustomCheckDeletedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckFailedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckSucceededDefinition>();
                serviceCollection.AddPlatformConnectionProvider<CustomChecksPlatformConnectionDetailsProvider>();
                serviceCollection.AddSingleton<CustomCheckResultProcessor>();

                switch (settings.DataStoreType)
                {
                    case DataStoreType.InMemory:
                        serviceCollection.AddSingleton<ICustomChecksDataStore, InMemoryCustomCheckDataStore>();
                        break;
                    case DataStoreType.RavenDb:
                        serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
                        break;
                    case DataStoreType.SqlDb:
                        serviceCollection.AddSingleton<ICustomChecksDataStore>(sp => new SqlDbCustomCheckDataStore(settings.SqlStorageConnectionString, sp.GetService<IDomainEvents>()));
                        break;
                    default:
                        break;
                }
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                context.RegisterInstallationTask(() => SqlDbCustomCheckDataStore.Setup(settings.SqlStorageConnectionString));
            }
        }
    }
}