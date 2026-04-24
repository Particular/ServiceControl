namespace ServiceControl.CustomChecks
{
    using Connection;
    using Contracts;
    using ExternalIntegrations;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class CustomChecksComponent : ServiceControlComponent
    {
        public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
        {
            // Integration Events
            if (!settings.DisableExternalIntegrationsPublishing)
            {
                context.AddEventPublished<CustomCheckFailed>();
                context.AddEventPublished<CustomCheckSucceeded>();
            }
        }

        public override void Configure(Settings settings, EndpointConfiguration endpointConfiguration, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Services.AddIntegrationEventPublisher<CustomCheckFailedPublisher>();
            hostBuilder.Services.AddIntegrationEventPublisher<CustomCheckSucceededPublisher>();
            hostBuilder.Services.AddEventLogMapping<CustomCheckDeletedDefinition>();
            hostBuilder.Services.AddEventLogMapping<CustomCheckFailedDefinition>();
            hostBuilder.Services.AddEventLogMapping<CustomCheckSucceededDefinition>();
            hostBuilder.Services.AddPlatformConnectionProvider<CustomChecksPlatformConnectionDetailsProvider>();
            hostBuilder.Services.AddSingleton<CustomCheckResultProcessor>();
        }
    }
}