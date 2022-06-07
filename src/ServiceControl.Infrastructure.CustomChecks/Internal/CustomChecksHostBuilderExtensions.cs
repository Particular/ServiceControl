namespace ServiceControl.CustomChecks.Internal
{
    using System.Linq;
    using Connection;
    using ExternalIntegrations;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Hosting;

    public static class CustomChecksHostBuilderExtensions
    {
        public static IHostBuilder UseCustomChecksWithForwarding(this IHostBuilder hostBuilder, string serviceName, string forwardTo)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddHostedService(provider => new InternalCustomChecksHostedService(
                    provider.GetServices<ICustomCheck>().ToList(),
                    provider.GetRequiredService<ICustomChecksBackend>(),
                    provider.GetRequiredService<HostInformation>(),
                    provider.GetRequiredService<IAsyncTimer>(),
                    serviceName));
                collection.AddSingleton<ICustomChecksBackend>(sc => new CustomChecksForwarder(sc.GetRequiredService<IMessageSession>(), forwardTo));
            });
            return hostBuilder;
        }

        public static IHostBuilder UseCustomChecks(this IHostBuilder hostBuilder, string serviceName, bool settingsDisableHealthChecks = false)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                if (!settingsDisableHealthChecks)
                {
                    collection.AddHostedService(provider => new InternalCustomChecksHostedService(
                        provider.GetServices<ICustomCheck>().ToList(),
                        provider.GetRequiredService<ICustomChecksBackend>(),
                        provider.GetRequiredService<HostInformation>(),
                        provider.GetRequiredService<IAsyncTimer>(),
                        serviceName));
                }

                collection.AddScoped<GetCustomChecksApi>();
                collection.AddScoped<DeleteCustomChecksApi>();

                collection.AddHostedService<CustomChecksHostedService>();
                collection.AddSingleton<ICustomChecksBackend, CustomChecksStorage>();

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