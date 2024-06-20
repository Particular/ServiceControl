namespace ServiceControl.ExternalIntegrations
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ExternalIntegrationsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization,
            IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();
        }
    }
}