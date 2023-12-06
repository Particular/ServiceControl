namespace ServiceControl.ExternalIntegrations
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class ExternalIntegrationsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();
        }
    }
}