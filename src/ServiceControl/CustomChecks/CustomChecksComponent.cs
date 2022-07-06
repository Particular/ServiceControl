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
                serviceCollection.AddHostedService<CustomChecksHostedService>();
                serviceCollection.AddSingleton<CustomChecksStorage>();

                serviceCollection.AddIntegrationEventPublisher<CustomCheckFailedPublisher>();
                serviceCollection.AddIntegrationEventPublisher<CustomCheckSucceededPublisher>();

                serviceCollection.AddEventLogMapping<CustomCheckDeletedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckFailedDefinition>();
                serviceCollection.AddEventLogMapping<CustomCheckSucceededDefinition>();


                serviceCollection.AddPlatformConnectionProvider<CustomChecksPlatformConnectionDetailsProvider>();
            });
        }
    }
}