namespace ServiceControl.CustomChecks
{
    using Connection;
    using ExternalIntegrations;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class CustomChecksComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
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