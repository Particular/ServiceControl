namespace ServiceControl.ExternalIntegrations
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class ExternalIntegrationsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(services =>
            {
                services.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
        }
    }
}