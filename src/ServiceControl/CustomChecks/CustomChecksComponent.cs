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

                switch (settings.DataStoreType)
                {
                    case DataStoreType.InMemory:
                        serviceCollection.AddSingleton<ICustomChecksStorage, InMemoryCustomCheckDataStore>();
                        break;
                    case DataStoreType.RavenDb:
                        serviceCollection.AddSingleton<ICustomChecksStorage, RavenDbCustomCheckDataStore>();
                        break;
                    case DataStoreType.SqlDb:
                        serviceCollection.AddSingleton<EndpointDetailsMapper>();
                        serviceCollection.AddSingleton<ICustomChecksStorage>(sp => new SqlDbCustomCheckDataStore(settings.SqlStorageConnectionString, sp.GetService<IDomainEvents>(), sp.GetService<EndpointDetailsMapper>()));
                        break;
                    default:
                        break;
                }
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                context.RegisterSetupTask(() => SqlDbCustomCheckDataStore.Setup(settings.SqlStorageConnectionString));
            }
        }
    }
}