namespace ServiceControl.CustomChecks
{
    using Connection;
    using ExternalIntegrations;
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
                serviceCollection.AddIntegrationEventPublisher<CustomCheckFailedPublisher>();
                serviceCollection.AddIntegrationEventPublisher<CustomCheckSucceededPublisher>();
                serviceCollection.AddEventLogMapping<CustomCheckDeletedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckFailedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckSucceededDefinition>();
                serviceCollection.AddPlatformConnectionProvider<CustomChecksPlatformConnectionDetailsProvider>();
                serviceCollection.AddSingleton<CustomCheckResultProcessor>();
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
            // TODO: Delete when dropping sql persister
            if (settings.DataStoreType == DataStoreType.SqlDb)
            {
                var connectionString = SettingsReader<string>.Read("SqlStorageConnectionString");
                context.RegisterInstallationTask(() => Persistence.SetupSqlPersistence.SetupCustomChecks(connectionString));
            }
        }
    }
}