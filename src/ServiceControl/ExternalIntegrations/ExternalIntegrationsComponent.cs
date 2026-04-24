namespace ServiceControl.ExternalIntegrations
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ExternalIntegrationsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();

            if (!settings.DisableExternalIntegrationsPublishing)
            {
                services.AddHostedService<EventDispatcherHostedService>();
                services.AddDomainEventHandler<IntegrationEventWriter>();
            }
        }
    }
}