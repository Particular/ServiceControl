namespace ServiceControl.CustomChecks.Internal
{
    using System.Linq;
    using Connection;
    using ExternalIntegrations;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Hosting;

    public static class CustomChecksHostBuilderExtensions
    {
        public static IHostBuilder UseCustomChecks(this IHostBuilder hostBuilder, string serviceName, bool settingsDisableHealthChecks = false)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                if (!settingsDisableHealthChecks)
                {
                    collection.AddHostedService(provider => new InternalCustomChecksHostedService(
                        provider.GetServices<ICustomCheck>().ToList(),
                        provider.GetRequiredService<CustomChecksStorage>(),
                        provider.GetRequiredService<HostInformation>(),
                        provider.GetRequiredService<IAsyncTimer>(),
                        serviceName));
                }

                collection.AddHostedService<CustomChecksHostedService>();
                collection.AddSingleton<CustomChecksStorage>();

                collection.AddIntegrationEventPublisher<CustomCheckFailedPublisher>();
                collection.AddIntegrationEventPublisher<CustomCheckSucceededPublisher>();

                collection.AddEventLogMapping<CustomCheckDeletedDefinition>();
                collection.AddEventLogMapping<CustomCheckFailedDefinition>();
                collection.AddEventLogMapping<CustomCheckSucceededDefinition>();

                collection.AddPlatformConnectionProvider<CustomChecksPlatformConnectionDetailsProvider>();
            });
            return hostBuilder;
        }
    }
}